using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine.UI;
using agora_gaming_rtc;
using System.Runtime.InteropServices;

public class Total : MonoBehaviour
{
    //Variable for handling Kinect
    Device kinect;
    //Width and Height of Depth adn RGB image.
    int depthWidth;
    int depthHeight;
    int rgbWidth;
    int rgbHeight;
    //Number of all points 
    int numRgb;
    int numDepth;
    //Array of colors corresponding to each point in PointCloud
    Color32[] colorsRgb;
    Color32[] colorsDepth;
    Color32[] colorsFinal;
    //Raw Image to display
    RawImage rawImageRgb;
    RawImage rawImageDepth;
    //Color image to be attatched to mesh
    Texture2D textureRgb;
    Texture2D textureDepth;
    Texture2D FinalTexture;
    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    Transformation transformation;
    public byte[] RawCalibration;
    public string appId = "Your_AppID";
    public string channelName = "agora";
    public IRtcEngine mRtcEngine;
    int i = 100;

    //Stop Kinect as soon as this object disappear
    private void OnDestroy()
    {
        kinect.StopCameras();
    }

    void Start()
    {
        //The method to initialize Kinect
        InitKinect();
        //Initialization for colored mesh rendering
        InitTexture();
        //Loop to get data from Kinect and rendering
        InitAgora();
    }

    void InitAgora()
    {
        Debug.Log("ScreenShare Activated");
        mRtcEngine = IRtcEngine.getEngine(appId);
        // enable log
        mRtcEngine.SetLogFilter(LOG_FILTER.DEBUG | LOG_FILTER.INFO | LOG_FILTER.WARNING | LOG_FILTER.ERROR | LOG_FILTER.CRITICAL);
        // set callbacks (optional)
        mRtcEngine.SetParameters("{\"rtc.log_filter\": 65535}");
        //Configure the external video source
        mRtcEngine.SetExternalVideoSource(true, false);
        // Start video mode
        mRtcEngine.EnableVideo();
        // allow camera output callback
        mRtcEngine.EnableVideoObserver();
        // join channel
        mRtcEngine.JoinChannel(channelName, null, 0);
    }

    void Update()
    {
        Capture capture = kinect.GetCapture();

        //Updating the depth capture to RGB image
        System.UInt16[] depthArray = capture.Depth.GetPixels<System.UInt16>().ToArray();
        //Encoding the depth Array
        colorsDepth = Encode(depthArray);

        textureDepth.SetPixels32(colorsDepth);
        textureDepth.Apply();

        //Updating the rgb image
        //BGRA[] colorArray = capture.Color.GetPixels<BGRA>().ToArray();

        ////Getting color information
        Microsoft.Azure.Kinect.Sensor.Image modifiedColor = transformation.ColorImageToDepthCamera(capture);
        BGRA[] colorArray = modifiedColor.GetPixels<BGRA>().ToArray();

        //Updating the rgb image
        for (int i = 0; i < colorArray.Length; i++)
        {
            colorsRgb[i].a = 255;
            colorsRgb[i].b = colorArray[i].B;
            colorsRgb[i].g = colorArray[i].G;
            colorsRgb[i].r = colorArray[i].R;
        }

        textureRgb.SetPixels32(colorsRgb);
        textureRgb.Apply();

        //Sending the RGB image

        //Merging the two images
        Combinate();

        // Get the Raw Texture data from the the from the texture and apply it to an array of bytes
        byte[] bytes = FinalTexture.GetRawTextureData();

        // Make enough space for the bytes array
        int size = Marshal.SizeOf(bytes[0]) * bytes.Length;

        // Check to see if there is an engine instance already created
        IRtcEngine rtc = IRtcEngine.QueryEngine();

        //if the engine is present
        Debug.Log("rtc is null?" + (rtc == null));
        if (rtc != null)
        {
            //Create a new external video frame
            ExternalVideoFrame externalVideoFrame = new ExternalVideoFrame();
            //Set the buffer type of the video frame
            externalVideoFrame.type = ExternalVideoFrame.VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA;
            // Set the video pixel format
            externalVideoFrame.format = ExternalVideoFrame.VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_RGBA;
            //apply raw data you are pulling from the rectangle you created earlier to the video frame
            externalVideoFrame.buffer = bytes;
            //Set the width of the video frame (in pixels)
            externalVideoFrame.stride = depthWidth;
            //Set the height of the video frame
            externalVideoFrame.height = 2*depthHeight;
            ////Remove pixels from the sides of the frame
            externalVideoFrame.cropLeft = 10;
            externalVideoFrame.cropTop = 10;
            externalVideoFrame.cropRight = 10;
            externalVideoFrame.cropBottom = 10;
            //Rotate the video frame (0, 90, 180, or 270)
            externalVideoFrame.rotation = 180;
            // increment i with the video timestamp
            externalVideoFrame.timestamp = i++;
            //Push the external video frame with the frame we just created
            int a = rtc.PushVideoFrame(externalVideoFrame);
            Debug.Log(" pushVideoFrame =       " + a);
        }
    }

    void Combinate()
    {
        colorsDepth.CopyTo(colorsFinal, 0);
        colorsRgb.CopyTo(colorsFinal, colorsDepth.Length);

        FinalTexture.SetPixels32(colorsFinal);
        FinalTexture.Apply();
    }

    //Initialization of Kinect
    void InitKinect()
    {
        //Connect with the 0th Kinect
        kinect = Device.Open(0);
        //Setting the Kinect operation mode and starting it
        kinect.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NFOV_2x2Binned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30,
        });
        //Access to coordinate transformation information
        transformation = kinect.GetCalibration().CreateTransformation();
    }

    //Prepare to draw colored mesh
    void InitTexture()
    {
        //Display Image
        rawImageRgb = GameObject.Find("Display rgb").GetComponent<RawImage>();
        rawImageDepth = GameObject.Find("Display depth").GetComponent<RawImage>();

        //Get the width and height of the Depth image and calculate the number of all points
        depthWidth = kinect.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        depthHeight = kinect.GetCalibration().DepthCameraCalibration.ResolutionHeight;
        rgbWidth = kinect.GetCalibration().ColorCameraCalibration.ResolutionWidth;
        rgbHeight = kinect.GetCalibration().ColorCameraCalibration.ResolutionHeight;
        numRgb = rgbWidth * rgbHeight;
        numDepth = depthWidth * depthHeight;
        int num_final = 2 * numDepth;

        //Allocation of vertex and color storage space for the total number of pixels in the depth image
        colorsRgb = new Color32[numDepth];
        textureRgb = new Texture2D(depthWidth, depthHeight);

        textureDepth = new Texture2D(depthWidth, depthHeight);
        colorsDepth = new Color32[numDepth];

        colorsFinal = new Color32[num_final];
        FinalTexture = new Texture2D(depthWidth, depthHeight + depthHeight);

        rawImageRgb.texture = FinalTexture;
        rawImageDepth.texture = textureDepth;
    }

    void OnApplicationQuit()
    {
        if (mRtcEngine != null)
        {
            IRtcEngine.Destroy();
            mRtcEngine = null;
        }
    }
    
    void Encode2(System.UInt16[] DepthArray)
    {
        for (int i = 0; i < DepthArray.Length; i++)
        {   
            byte BIT_MASK = (byte)0xff;   // low 8 bits
            colorsDepth[i].b = (byte)(DepthArray[i] & BIT_MASK);
            colorsDepth[i].g = (byte)(DepthArray[i] >> 8); // high 8 bits
            colorsDepth[i].a = 255;
            colorsDepth[i].r = 0; 
        }
    }

    Color32[] Encode(System.UInt16[] DepthArray)
    {
        Color32[] colorsDepth = new Color32[numDepth];
        // blue, cyan, green, yellow, red, magenta 
        for (int i = 0; i < DepthArray.Length; i++)
        {
            if ((DepthArray[i] > 500) & (DepthArray[i] < 1000))
            {
                colorsDepth[i].a = (byte)255;
                colorsDepth[i].b = (byte)(255 * (DepthArray[i] - 500) / 500);
                colorsDepth[i].g = 0;
                colorsDepth[i].r = 0;
            }
            if ((DepthArray[i] > 1000) & (DepthArray[i] < 1500))
            {
                colorsDepth[i].a = (byte)255;
                colorsDepth[i].b = (byte)255;
                colorsDepth[i].g = (byte)(255 * (DepthArray[i] - 1000) / 500);
                colorsDepth[i].r = 0;
            }
            if ((DepthArray[i] > 1500) & (DepthArray[i] < 2000))
            {
                colorsDepth[i].a = (byte)255;
                colorsDepth[i].b = 0;
                colorsDepth[i].g = (byte)(255 * (DepthArray[i] - 1500) / 500);
                colorsDepth[i].r = 0;
            }
            if ((DepthArray[i] > 2000) & (DepthArray[i] < 2500))
            {
                colorsDepth[i].a = 255;
                colorsDepth[i].b = 0;
                colorsDepth[i].g = 255;
                colorsDepth[i].r = (byte)(255 * (DepthArray[i] - 2000) / 500);
            }
            if ((DepthArray[i] > 2500) & (DepthArray[i] < 3000))
            {
                colorsDepth[i].a = 255;
                colorsDepth[i].b = 0;
                colorsDepth[i].g = 0;
                colorsDepth[i].r = (byte)(255 * (DepthArray[i] - 2500) / 500);
            }
            if ((DepthArray[i] > 3000))
            {
                colorsDepth[i].a = 255;
                colorsDepth[i].b = 255;
                colorsDepth[i].g = 0;
                colorsDepth[i].r = 255;
            }
        }
        return colorsDepth;
    }

}
