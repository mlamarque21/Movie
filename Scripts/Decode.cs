using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using agora_gaming_rtc;
using Microsoft.Azure.Kinect.Sensor;
using K4AdotNet.Sensor;

public class Decode : MonoBehaviour
{
    public Texture2D textureRgb;
    public Texture2D textureDepth;
    Texture2D RemoteTexture;
    VideoSurface RemoteView;
    Color32[] colorsDepth;
    short[] depthArray;
    AgoraChat agoratamere;
    //Number of all points of PointCloud 
    int numDepth;
    //Size of the Depth Capture
    int depthWidth;
    int depthHeight;
    //Used to draw a set of points
    public Mesh mesh;
    //Array of coordinates for each point in PointCloud
    Vector3[] vertices;
    //Array of colors corresponding to each point in PointCloud
    Color32[] colors;
    //List of indexes of points to be rendered
    int[] indices;
    //Regenerating Depth Capture
    K4AdotNet.Sensor.Image DepthCapture;
    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    K4AdotNet.Sensor.Transformation transformation;

    private void Awake()
    {
        InitTexture();
        InitUI();
        InitMesh();
        InitCallibration();
    }

    void InitCallibration()
    {
        byte[] RawCalibration = GameObject.Find("MKVStream0").GetComponent<MKVplayer>().rawcalib;
        K4AdotNet.Sensor.Calibration c;
        K4AdotNet.Sensor.Calibration.CreateFromRaw(RawCalibration, K4AdotNet.Sensor.DepthMode.NarrowView2x2Binned,  K4AdotNet.Sensor.ColorResolution.R720p, out c);
        //Generate transformation
        transformation = c.CreateTransformation();
    }

    void InitUI()
    {
        GameObject.Find("depth").GetComponent<RawImage>().texture = textureDepth;
        GameObject.Find("rgb").GetComponent<RawImage>().texture = textureRgb;
        agoratamere = GameObject.Find("GameController").GetComponent<AgoraChat>();
    }

    void InitTexture()
    {
        depthWidth = 320;
        depthHeight = 288;
        numDepth = depthWidth * depthHeight;
        textureRgb = new Texture2D(depthWidth, depthHeight);
        textureDepth = new Texture2D(depthWidth, depthHeight);
        colorsDepth = new Color32[numDepth];
        depthArray = new short[numDepth];
        DepthCapture = new K4AdotNet.Sensor.Image(K4AdotNet.Sensor.ImageFormat.Depth16, depthWidth, depthHeight);
    }

    void InitMesh()
    {
        //Instantiate mesh
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        //Allocation of vertex and color storage space for the total number of pixels in the depth image
        vertices = new Vector3[numDepth];
        colors = new Color32[numDepth];
        indices = new int[numDepth];

        //Initialization of index list
        for (int i = 0; i < numDepth; i++)
        {
            indices[i] = i;
        }

        //Allocate a list of point coordinates, colors, and points to be drawn to mesh
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        gameObject.GetComponent<MeshFilter>().mesh = mesh;
    }

    void Separate()
    {
        GameObject go = GameObject.Find("GameController");
        RemoteView = go.GetComponent<VideoSurface>();
        RemoteTexture = RemoteView.nativeTexture;
        Color[] depthPixels = RemoteTexture.GetPixels(0, depthWidth, depthWidth, depthHeight);
        Color[] rgbPixels = RemoteTexture.GetPixels(0, 0, depthWidth, depthHeight);

        //Reset the mesh vertices
        vertices = new Vector3[numDepth];


        // Decoding it into depthCapture
        for (int i=0; i<numDepth; i++)
        {   
            int red = (int)(depthPixels[i].r * 255);
            int blue = (int)(depthPixels[i].b * 255);
            int green = (int)(depthPixels[i].g * 255);

            if ((red == 0) & (green == 0))
            {
                depthArray[i] = (short)((500 * depthPixels[i].b)  + 500);
            }

            else if ((red == 0) & (blue == 255))
            {
                depthArray[i] = (short)((500 * depthPixels[i].g) + 1000);
            }

            else if ((red == 0) & (blue == 0))
            {
                depthArray[i] = (short)((500 * depthPixels[i].g)  + 1500);
            }

            else if ((green == 255) & (blue == 0))
            {
                depthArray[i] = (short)((500 * depthPixels[i].r)  + 2000);
            }

            else if ((green == 0) & (blue == 0))
            {
                depthArray[i] = (short)((500 * depthPixels[i].r) + 2500);
            }
            // check encoding of depth.value > 3000 in the encoding file 
            else if ((green == 0) & (blue == 0))
            {
                depthArray[i] = (short)((500 * depthPixels[i].b) + 2500);
            }

            // if (i%50 == 0)
            // {
            //     Debug.Log(depthArray[i]);
            // }
        }

        DepthCapture.FillFrom(depthArray);

        //Updating the rgb Image
        textureRgb.SetPixels(rgbPixels);
        textureRgb.Apply();

        //Updating the depth Image
        textureDepth.SetPixels(depthPixels);
        textureDepth.Apply();

        //Getting vertices of point cloud

        //Préparation buffer Depth 
        short[] xyzImageBuffer = new short[numDepth * 3];
        int xyzImageStride = depthWidth * sizeof(short) * 3;

        //Remplit le xyzBuffer avec les �l�ments de la transformation Depth to PC
        K4AdotNet.Sensor.Image xyzImage = K4AdotNet.Sensor.Image.CreateFromArray(xyzImageBuffer, K4AdotNet.Sensor.ImageFormat.Custom, DepthCapture.WidthPixels, DepthCapture.HeightPixels, xyzImageStride);
        transformation.DepthImageToPointCloud(DepthCapture, CalibrationGeometry.Depth, xyzImage);


        //Rendering the point cloud
        for (int index = 0; index < numDepth; index++)
        {   
            //Removing the black outline
            if (depthArray[index] != 500)
            {
            vertices[index].x = xyzImageBuffer[index*3] * 0.001f;
            vertices[index].y = -xyzImageBuffer[index*3 + 1] * 0.001f;//上下反転
            vertices[index].z = xyzImageBuffer[index*3 + 2] * 0.001f;

            colors[index] = rgbPixels[index];
            }
        }

        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.RecalculateBounds();
    }

    // Update is called once per frame
    void Update()
    {   
        if (agoratamere.joined)
        {
            Separate();
        }
    }
}