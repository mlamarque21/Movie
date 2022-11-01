using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using agora_gaming_rtc;
using UnityEngine.UI;

public class AgoraChat : MonoBehaviour
{

    public string AppID;
    public string ChannelName;
    public bool joined = false;
    VideoSurface remoteView;
    IRtcEngine mRtcEngine;

    void SetupUI()
    {
        GameObject go = GameObject.Find("LeaveButton");
        go?.GetComponent<Button>()?.onClick.AddListener(Leave);
        go = GameObject.Find("JoinButton");
        go?.GetComponent<Button>()?.onClick.AddListener(Join);
    }


    void Awake()
    {
        SetupUI();
    }

    void SetupAgora()
    {
        mRtcEngine = IRtcEngine.GetEngine(AppID);

        mRtcEngine.OnUserJoined = OnUserJoined;
        mRtcEngine.OnUserOffline = OnUserOffline;
        mRtcEngine.OnJoinChannelSuccess = OnJoinChannelSuccessHandler;
        mRtcEngine.OnLeaveChannel = OnLeaveChannelHandler;
    }

    void Start()
    {
        SetupAgora();
    }

    void Join()
    {
        mRtcEngine.EnableVideo();
        mRtcEngine.EnableVideoObserver();
        mRtcEngine.JoinChannel(ChannelName, "", 0);
    }

    void Leave()
    {
        mRtcEngine.LeaveChannel();
        mRtcEngine.DisableVideo();
        mRtcEngine.DisableVideoObserver();
    }

    void OnJoinChannelSuccessHandler(string channelName, uint uid, int elapsed)
    {
        // can add other logics here, for now just print to the log
        Debug.LogFormat("Joined channel {0} successful, my uid = {1}", channelName, uid);
    }

    void OnLeaveChannelHandler(RtcStats stats)
    {
        if (remoteView != null)
        {
            remoteView.SetEnable(false);
        }
    }

    void OnUserJoined(uint uid, int elapsed)
    {
        GameObject go = GameObject.Find("GameController");
        if (remoteView == null)
        {
            remoteView = go.AddComponent<VideoSurface>();
        }

        go.transform.Rotate(0.0f, 0.0f, -180.0f);

        remoteView.SetForUser(uid);
        remoteView.SetEnable(true);
        remoteView.SetVideoSurfaceType(AgoraVideoSurfaceType.Renderer);
        joined = true;
    }


    void OnUserOffline(uint uid, USER_OFFLINE_REASON reason)
    {
        remoteView.SetEnable(false);
    }

    void OnApplicationQuit()
    {
        if (mRtcEngine != null)
        {
            IRtcEngine.Destroy();
            mRtcEngine = null;
        }
    }


}
