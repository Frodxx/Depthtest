using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Kinect.Tools;
using Microsoft.Kinect.Wpf;

namespace DepthTest

{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
   
    public partial class MainWindow : Window
    {
        private readonly int _bytePerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private KinectSensor _kinect = null;
        private DepthFrameReader _depthReader = null;
        private byte[] _depthPixels = null;
        private ushort[] _depthData = null;
        private WriteableBitmap _depthBitmap = null;


        public MainWindow()
        {
            InitializeComponent();

            InitializeKinect();  // inicia kinect con sensor depth

            Closing += OnClosing;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_kinect != null)
            {
                _kinect.Close();
            }
        }

        #region Initialize
        private void InitializeKinect()
        {  
            _kinect = KinectSensor.GetDefault();
            _kinect.Open();

            FrameDescription desc = _kinect.DepthFrameSource.FrameDescription;
            _depthReader = _kinect.DepthFrameSource.OpenReader();
            _depthData = new ushort[desc.Width * desc.Height];
            _depthPixels = new byte[desc.Width * desc.Height * _bytePerPixel];
            _depthBitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgr32, null);
            DepthImage.Source = _depthBitmap;
            _depthReader.FrameArrived += OnDepthFrameArrived;

            
        }
        #endregion Initialize

        #region Convert HSL to RGB
        

        private void Convert(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R = 0.0, G = 0.0, B = 0.0;

            if (V <= 0)
            {
                R = G = B = 0;
            }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {
                    //red dominant

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    //green dominant

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;

                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    //blue dominant

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;

                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    //red dominant

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;
                    //justincase
                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    default:
                        break;

                }
            }

            r = Clamp((int)(R * 255.0));
            g = Clamp((int)(G * 255.0));
            b = Clamp((int)(B * 255.0));

        }


        int Clamp(int j)
        {
            if (j < 0) { return 0; }

            else if (j > 255) { return 255; }
            else { return j; }
            
        }

        #endregion Convert HSL to RGB

        #region Depththings
        private void OnDepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            DepthFrameReference refer = e.FrameReference;

            if (refer == null)
            {
                return;
            }
         
            DepthFrame frame = refer.AcquireFrame();
            if (frame == null)
            {
                return;
            }

            using (frame)
            {
                FrameDescription frameDesc = frame.FrameDescription;

                if(((frameDesc.Width * frameDesc.Height) == _depthData.Length)&& (frameDesc.Width == _depthBitmap.PixelWidth) && (frameDesc.Height == _depthBitmap.PixelHeight))
                {
                    
                    frame.CopyFrameDataToArray(_depthData);

                    ushort minDepth = frame.DepthMinReliableDistance;
                    ushort maxDepth = frame.DepthMaxReliableDistance;
                    
                    int colorPixelIndex = 0;
                    for (int i = 0; i < _depthData.Length; i++)
                    {

                        double depth = _depthData[i];
                        double h, s, v;
                        int r = 0, g = 0, b = 0;

                        h = (((depth - minDepth) / (1150 - minDepth)) * 360);
                        v = 1;
                        s = 1;
                        
                        Convert(h, s, v, out r, out g, out b);
                 
                        
                        if (depth == 0)
                        {
                            _depthPixels[colorPixelIndex++] = 0;//41;
                            _depthPixels[colorPixelIndex++] = 0;//239;
                            _depthPixels[colorPixelIndex++] = 0;//242;
                        }
                        else if (depth < minDepth || depth > maxDepth)
                        {
                            _depthPixels[colorPixelIndex++] = 255;//25;
                            _depthPixels[colorPixelIndex++] = 255;//0;
                            _depthPixels[colorPixelIndex++] = 255;//255;
                        }
                        
                         else
                        {
                            _depthPixels[colorPixelIndex++] = (byte)b;
                            _depthPixels[colorPixelIndex++] = (byte)g;
                            _depthPixels[colorPixelIndex++] = (byte)r;
                        }
                    
                        ++colorPixelIndex;
                  
                    }

                    _depthBitmap.WritePixels(
                           new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                           _depthPixels,
                           frameDesc.Width * _bytePerPixel,
                           0);                
                }
            }
        }
        #endregion Depththings
        #region UIthings



        #endregion UIthings
    }
}
