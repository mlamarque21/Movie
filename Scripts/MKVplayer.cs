using UnityEngine;
using K4AdotNet.Record;
using K4AdotNet.Sensor;

public class MKVplayer : MonoBehaviour
{   
    public byte[] rawcalib;
    public Object MKV;
    public Playback mkvStream;
    public Calibration calib;
    // Start is called before the first frame update
    void Start()
    {
        PrepareMKVsFilesToStream();
    }

    private void PrepareMKVsFilesToStream()
    {
        mkvStream = new Playback(Application.streamingAssetsPath + "/" + MKV.name + ".mkv");
        mkvStream.GetCalibration(out calib);
        rawcalib = mkvStream.GetRawCalibration();
    }
}
