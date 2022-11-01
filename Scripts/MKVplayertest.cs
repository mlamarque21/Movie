using System.Collections;
using System.Collections.Generic;
//using System.Collections.Generic;
using UnityEngine;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using K4AdotNet;
using System.Threading.Tasks;
//using System.IO;

public class MKVplayertest : MonoBehaviour
{
    public Object MKV;
    private Mesh mesh;
    public Playback mkvStream;
    RecordConfiguration config;
    public Calibration calib;
    private int framerate;
    PlaybackSeekOrigin origin;

    public Transformation transfor;

    public static int frame = 0;

    int[] indices;

    int width;
    int height;
    int num;
    Vector3[] vertices;
    Color32[] colors;
    public bool colorToDepth;

    private Task t;

    // Start is called before the first frame update
    void Start()
    {
        PrepareMKVsFilesToStream();

        //initialisation du point cloud mesh
        InitMesh();

        t = MKVLoop();
    }

    private void PrepareMKVsFilesToStream()
    {
        //pr�paration du MKV stream et des param�tres K4adotnet
        //Debug.Log(MKV.ToString());
        Debug.Log(MKV.name);
        //mkvPath = AssetDatabase.GetAssetPath(MKV);
        mkvStream = new Playback(Application.streamingAssetsPath + "/" + MKV.name + ".mkv");
        mkvStream.GetRecordConfiguration(out config);
        framerate = config.CameraFps.ToNumberHz();
        mkvStream.GetCalibration(out calib);
        origin = PlaybackSeekOrigin.Begin;
        transfor = calib.CreateTransformation();

        //param�tres du fixed update

        Application.targetFrameRate = 120;
        Time.fixedDeltaTime = 1 / (float)framerate;
        Debug.Log("fixedDeltaTime : " + 1 / (float)framerate);
    }


    private void InitMesh()
    {
        if (colorToDepth)
        {
            width = calib.DepthCameraCalibration.ResolutionWidth;
            height = calib.DepthCameraCalibration.ResolutionHeight;
            Debug.Log(width);
        }

        else
        {
            width = calib.ColorCameraCalibration.ResolutionWidth;
            height = calib.ColorCameraCalibration.ResolutionHeight;
        }
        num = width * height;

        Debug.Log("width : " + width + " height : " + height + " num : " + num);
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        vertices = new Vector3[num];
        colors = new Color32[num];
        indices = new int[num];

        for (int i = 0; i < num; i++)
        {
            indices[i] = i;
        }
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        Debug.Log("framerate : " + framerate);
        

    }
    void FixedUpdate()
    {
        if (this.name == "MKVStream0") frame += 1;
        if (t.IsFaulted) t = MKVLoop(); // en cas de plantage de MKVloop on le relance 

        //Color.RGBToHSV(lowerColor, out lowH, out lowS, out lowV);

        //Color.RGBToHSV(upperColor, out upperH, out upperS, out upperV);
        //Task t = MKVLoop(); //activer pour booster
        //Debug.Log(frame);
    }
    public async Task MKVLoop()
    {
        //if (Application.isPlaying) { }
        while (true) //commenter pour booster
        {   //Retourne la capture correspondante � la frame incr�ment�e en FixedUpdate dans le MKV
            //Debug.Log(frame + this.name);
            using (Capture capture = await Task.Run(() =>
            {
                //Task.Delay((int)Mathf.Round((1 / (float)framerate) * 1000)).Wait(); // pas S�r du tout du tout !


                if ((K4AdotNet.Microseconds64)(((float)frame - 0.5f) * 1000000 / (float)framerate) > mkvStream.RecordLength)
                    frame = 1; //loop system

                Capture cap;
                if ((K4AdotNet.Microseconds64)(((float)frame - 0.5f) * 1000000 / (float)framerate) < mkvStream.RecordLength)
                {
                    mkvStream.SeekTimestamp((K4AdotNet.Microseconds64)(((float)frame - 0.5f) * 1000000 / (float)framerate), origin);

                    mkvStream.SetColorConversion(ImageFormat.ColorBgra32);
                    mkvStream.TryGetNextCapture(out cap);
                }
                else cap = null;
                return cap;

            }))
            {

                //UnityEngine.Debug.Log("Capture : " + capture);

                // R�cup�re les color et depth de la capture

                Image colorImage = capture.ColorImage;
                Image depthImage = capture.DepthImage;
                //Debug.Log("exposure : "+colorImage.Exposure);
                /*
                if (depthImage == null)
                {
                    Debug.Log("file name : " + mkvPath.ToString());
                    Debug.Log("Depth frame number missing : " + frame);
                }
                if (colorImage == null)
                {
                    Debug.Log("file name : " + mkvPath.ToString());
                    Debug.Log("Color frame number missing : " + frame);
                }*/

                if (colorImage != null && depthImage != null)
                {

                    //Pr�paration buffer Color 

                    Color32[] ColorImageBuffer = new Color32[num];
                    int ColorImageStride = width * sizeof(byte) * 4;

                    //Pr�paration buffer Depth 

                    short[] xyzImageBuffer = new short[num * 3];
                    int xyzImageStride = width * sizeof(short) * 3;


                    if (colorToDepth)
                    {   
                        Debug.Log(depthImage.WidthPixels);
                        Debug.Log(depthImage.HeightPixels);
                        //Remplit le ColorImageBuffer avec les �l�ments de la transformation Color to Depth
                        using (Image ColortoDepthImage = Image.CreateFromArray(ColorImageBuffer, ImageFormat.ColorBgra32, depthImage.WidthPixels, depthImage.HeightPixels, ColorImageStride))
                        {
                            transfor.ColorImageToDepthCamera(depthImage, colorImage, ColortoDepthImage);
                        }

                        //Remplit le xyzBuffer avec les �l�ments de la transformation Depth to PC   
                        using (Image xyzImage = Image.CreateFromArray(xyzImageBuffer, ImageFormat.Custom, depthImage.WidthPixels, depthImage.HeightPixels, xyzImageStride))
                        {
                            transfor.DepthImageToPointCloud(depthImage, CalibrationGeometry.Depth, xyzImage);
                        }
                    }

                    else
                    {

                        Image DepthToColorImage = new Image(ImageFormat.Depth16, colorImage.WidthPixels, colorImage.HeightPixels);
                        transfor.DepthImageToColorCamera(depthImage, DepthToColorImage);

                        using (Image xyzImage = Image.CreateFromArray(xyzImageBuffer, ImageFormat.Custom, colorImage.WidthPixels, colorImage.HeightPixels, xyzImageStride))
                        {

                            transfor.DepthImageToPointCloud(DepthToColorImage, CalibrationGeometry.Color, xyzImage);
                        }

                        Texture2D ColorImg = new Texture2D(colorImage.WidthPixels, colorImage.HeightPixels, TextureFormat.BGRA32, false);
                        ColorImg.LoadRawTextureData(colorImage.Buffer, colorImage.SizeBytes);



                        ColorImageBuffer = ColorImg.GetPixels32();
                        ColorImg.Apply();
                    }

                    for (int i = 0; i < num; i++)
                    {

                        vertices[i].x = xyzImageBuffer[i * 3] * 0.001f;
                        vertices[i].y = -xyzImageBuffer[i * 3 + 1] * 0.001f;
                        vertices[i].z = xyzImageBuffer[i * 3 + 2] * 0.001f;

                        if (colorToDepth)
                        {
                            colors[i].r = ColorImageBuffer[i].b;
                            colors[i].b = ColorImageBuffer[i].r;
                        }
                        else
                        {
                            colors[i].r = ColorImageBuffer[i].r;
                            colors[i].b = ColorImageBuffer[i].b;
                        }

                        colors[i].g = ColorImageBuffer[i].g;

                        colors[i].a = 255;

                    }

                    mesh.vertices = vertices;
                    mesh.colors32 = colors;
                    mesh.RecalculateBounds();
                }
            }
        }
    }
}
