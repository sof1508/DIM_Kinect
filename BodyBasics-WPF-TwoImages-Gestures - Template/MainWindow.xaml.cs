//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Kinect.BodyStream
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;


        

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Current body renderer
        /// </summary>
        private BodyRenderer bodyRenderer = null;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
               return bodyRenderer.imageSource;
            }
        }



        private DepthFrameReader depthFrameReader;
        // Color Data Structures
        private byte[] depthPixels = null;
        private WriteableBitmap bitmap = null;
        private const int BytesPerPixel = 4;

        private ushort[] depthFrameData;
        private bool isDepthShown=true;

        //gesture detectors and event raiser
        private GestureResultView result;
        private GestureDetector detector;

        private int num_events = 0;
        private DateTime prevTime;
        private DateTime currTime;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();


            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            
            //creates a body renderer
            this.bodyRenderer = new BodyRenderer(this.kinectSensor.CoordinateMapper, frameDescription.Width, frameDescription.Height);

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            //depth reader and data structures
            depthFrameReader = kinectSensor.DepthFrameSource.OpenReader();
            depthFrameReader.FrameArrived += Reader_DepthSourceFrameArrived;
            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            this.depthFrameData = new ushort[depthFrameDescription.Width *
                                    depthFrameDescription.Height];

            this.depthPixels =
                new byte[depthFrameDescription.Width *
                  depthFrameDescription.Height * BytesPerPixel];
            // create the bitmap to display
            this.bitmap =
                new WriteableBitmap(depthFrameDescription.Width,
                depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;


            /// <summary>
            /// Tarea a realizar por alumno
            /// Inicializar detector de gestos y listeners
            /// </summary>
            /// /////////////////////////////////////////////////////////////////////////////////////////////////
            result = new GestureResultView(0, "", false, false, 0.0f);
            detector = new GestureDetector(this.kinectSensor, result);
            result.PropertyChanged += GestureResult_PropertyChanged;
            

            
            prevTime = DateTime.Now;

            // initialize the components (controls) of the window
            this.InitializeComponent();           
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;


        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            

            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {              
                    bodyRenderer.render(bodies);

                
                for (int i = 0; i < bodies.Length; ++i)
                {
                    if (bodies[i].IsTracked == true)
                    {
                        this.detector.TrackingId = bodies[i].TrackingId;
                        this.detector.IsPaused = false;
                    }
                }
                

            }
        }

        private void Reader_DepthSourceFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {

            
            using (DepthFrame depthFrame =
                 e.FrameReference.AcquireFrame())
            {
               // if (isDepthShown)
                    //ShowDepthFrame(depthFrame);
            }

        }

        private void ShowDepthFrame(DepthFrame depthFrame)
        {
            bool depthFrameProcessed = false;
            ushort minDepth = 0;
            ushort maxDepth = 0;

            if (depthFrame != null)
            {
                FrameDescription depthFrameDescription =
                    depthFrame.FrameDescription;

                // verify data and write the new infrared frame data
                // to the display bitmap
                if (((depthFrameDescription.Width * depthFrameDescription.Height)
                    == this.depthFrameData.Length) &&
                    (depthFrameDescription.Width == this.bitmap.PixelWidth) &&
                    (depthFrameDescription.Height == this.bitmap.PixelHeight))
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyFrameDataToArray(this.depthFrameData);

                    minDepth = depthFrame.DepthMinReliableDistance;
                    maxDepth = depthFrame.DepthMaxReliableDistance;

                    depthFrameProcessed = true;
                }
            }

            // we got a frame, convert and render
            if (depthFrameProcessed)
            {
                ConvertDepthDataToPixels(minDepth, maxDepth);
                RenderPixelArray(this.depthPixels);
            }
        }

        private void ConvertDepthDataToPixels(ushort minDepth, ushort maxDepth)
        {
            int colorPixelIndex = 0;
            // Shape the depth to the range of a byte
            int mapDepthToByte = maxDepth / 256;

            for (int i = 0; i < this.depthFrameData.Length; ++i)
            {
                // Get the depth for this pixel
                ushort depth = this.depthFrameData[i];

                // To convert to a byte, we're mapping the depth value
                // to the byte range.
                // Values outside the reliable depth range are 
                // mapped to 0 (black).
                byte intensity = (byte)(depth >= minDepth &&
                    depth <= maxDepth ? (depth / mapDepthToByte) : 0);

                this.depthPixels[colorPixelIndex++] = intensity; //Blue
                this.depthPixels[colorPixelIndex++] = intensity; //Green
                this.depthPixels[colorPixelIndex++] = intensity; //Red
                this.depthPixels[colorPixelIndex++] = 255; //Alpha
            }
        }

        private void RenderPixelArray(byte[] pixels)
        {
            this.bitmap.WritePixels(
               new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight),
               pixels,
               this.bitmap.PixelWidth * sizeof(int),
               0);
            displayColorImage.Source = this.bitmap;
        }
        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }



        void GestureResult_PropertyChanged(object sender,PropertyChangedEventArgs e)
        {
            GestureResultView result = sender as GestureResultView;

            if (e.PropertyName.Equals("Detected") && result.Detected &&(!result.GestureName.Equals("")))
            {
                
                currTime=DateTime.Now;
                TimeSpan diff = currTime - prevTime;

                this.StatusText = result.GestureName + " " + num_events+ " Seconds: "+diff.TotalSeconds;
                prevTime = currTime;
                num_events++;
                if (diff.TotalSeconds > 1)
                {
                    if (result.GestureName.Equals("PalmPunch_Right"))     //Reproducir
                    {
                        mediaElement.Play();
                    }
                    else
                    if (result.GestureName.Equals("PalmPunch_Left"))     //Pausar
                    {
                        mediaElement.Pause();
                    }
                    else
                    if (result.GestureName.Equals("DoublePalm"))     //Stop
                    {
                        mediaElement.Stop();
                    }
                    else
                    if (result.GestureName.Equals("SwipeHand_Right"))     //Acelerar velocidad de reproducción
                    {
                        int newmili = (int)mediaElement.Position.TotalMilliseconds + 5000;
                        if (newmili < mediaElement.NaturalDuration.TimeSpan.TotalMilliseconds)
                        {
                            TimeSpan ts = new TimeSpan(0, 0, 0, 0, newmili);
                            mediaElement.Position = ts;
                        }
                    }
                    else
                    if (result.GestureName.Equals("SwipeHand_Left"))      //Decrementar velocidad de reproducción
                    {
                        int newmili = (int)mediaElement.Position.TotalMilliseconds - 5000;
                        if (newmili > TimeSpan.Zero.TotalMilliseconds)
                        {
                            TimeSpan ts = new TimeSpan(0, 0, 0, 0, newmili);
                            mediaElement.Position = ts;
                        }                        
                    }
                    else
                    if (result.GestureName.Equals("RaiseArm_Right"))     //Subir volumen, no puede ser mayor que 1
                    {
                        if (volumeSlider.Value != 1)
                        {
                            volumeSlider.Value = volumeSlider.Value + 0.1;
                            mediaElement.Volume = (double)volumeSlider.Value;
                        }
                    }
                    else
                    if (result.GestureName.Equals("RaiseArm_Left"))      //Bajar volumen, no puede ser menor que 0
                    {
                        if (volumeSlider.Value != 0)
                        {
                            volumeSlider.Value  = volumeSlider.Value - 0.1;
                            mediaElement.Volume = volumeSlider.Value;

                        }
                    }
                    
                }

            }
            
        }


    }
}
