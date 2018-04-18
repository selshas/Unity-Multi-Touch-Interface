using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

public class UI_TouchInterface : MonoBehaviour {

    public bool isAreaLocked = false;
    public Vector2 areaLockStart = new Vector2(0, 0);
    public Vector2 areaLockSize = new Vector2(0, 0);
    private Rect lockedArea;

    private Canvas canvas;
    public Camera mainCam;

    public Vector2 currCenterCoord  = new Vector2(0, 0);
    public Vector2 prevCenterCoord  = new Vector2(0, 0);

    public Vector2 deltaPosition = new Vector2(0, 0);   //delta position of center coordinate between frames.
    public Vector2 deltaPositionTotal = new Vector2(0, 0);//delta position of center coordinate from when the touch started.

    private float initialBorderLength = 0;
    private float borderLength = 0; //length of border line at current frame

    private float prevBorderDelta = 0;
    private float borderDelta = 0;
    
    public float deltaScale = 1.0f;     //delta scale between frames.
    public float deltaScaleTotal = 0f;    //delta scale from when the touch started.

    private float prevRadianRotation;   //rotation at previous frame.
    private float currRadianRotation;   //rotation at current frame.

    public float deltaRotation = 0.0f;      //delta rotation between frames.
    public float deltaRotationTotal = 0.0f;   //delta rotation from when the touch started.

    public ushort tabCount = 0;

    public  ushort  maxTouchCount = 5;      //maximum limit of touch count.
    private Touch[] touches;

    private Vector3 relativeTouch0Position;

    private GameObject[] touchpoints;
    private GameObject   centerPoint;

    public bool debugMode = false;

    private ushort prevActiveTouchCount = 0;
    private ushort activeTouchCount     = 0;


    public void removeAreaLock()
    {
        isAreaLocked = false;
    }
    public bool setAreaLock(Vector2 areaStart, Vector2 areaSize)
    {
        isAreaLocked = true;
        Vector2 areaEnd = areaStart + areaSize;
        if ((areaEnd.x < 0 || Screen.width < areaEnd.x) || (areaEnd.y < 0 || Screen.height < areaEnd.y))
        {
            throw new System.Exception("Area size is over the screen.");
        }
        lockedArea = new Rect(areaStart, areaSize);
        Debug.Log(lockedArea.size);
        Debug.Log(areaStart);
        Debug.Log(areaSize);
        return true;
    }

    public bool setAreaLock()
    {
        return setAreaLock(new Vector2(0,0), new Vector2(Screen.width, Screen.height));
    }

    // Use this for initialization
    private void Awake()
    {
        touches = new Touch[maxTouchCount];
        touchpoints = new GameObject[maxTouchCount];

        canvas = gameObject.GetComponentInParent<Canvas>();

        if (isAreaLocked) {
            if (areaLockSize.magnitude > 0)
                setAreaLock(areaLockStart, areaLockSize);
            else
                setAreaLock();
        }

        if (debugMode)
        {
            //add center point visualizations
            centerPoint = new GameObject("touch center Point");

            centerPoint.AddComponent<Image>();
            Image tcp_img = centerPoint.GetComponent<Image>();
            tcp_img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            tcp_img.color = new Color32(255,0,0,255);

            RectTransform tcp_transform = centerPoint.GetComponent<RectTransform>();
            centerPoint.transform.SetParent(canvas.transform);
            tcp_transform.anchorMax = new Vector2(0, 0);
            tcp_transform.anchorMin = new Vector2(0, 0);

            //add touch point visualizations
            for (int i = 0; i < maxTouchCount; i++)
            {
                GameObject touchpoint = new GameObject("touch point"+i.ToString());
                touchpoint.AddComponent<Image>();
                Image tp_img = touchpoint.GetComponent<Image>();
                tp_img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

                GameObject touchPointIndex = new GameObject("index text");
                touchPointIndex.AddComponent<Text>();
                Text tp_text = touchPointIndex.GetComponent<Text>();
                tp_text.text = i.ToString();
                tp_text.alignment = TextAnchor.MiddleCenter;
                tp_text.fontSize = 24;
                tp_text.fontStyle = FontStyle.Bold;
                tp_text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                touchPointIndex.transform.localPosition = new Vector3(0,80.0f,0);
                touchPointIndex.transform.SetParent(touchpoint.transform);

                RectTransform tp_transform = touchpoint.GetComponent<RectTransform>();
                tp_transform.SetParent(canvas.transform);
                tp_transform.anchorMax = new Vector2(0, 0);
                tp_transform.anchorMin = new Vector2(0, 0);

                touchpoints[i] = touchpoint;
            }

        }
    }

	// Update is called once per frame
	void Update ()
    {
        List<Touch> touchList = new List<Touch>();

        //save previous CenterCoord, initialize new CenterCoord
        prevCenterCoord = currCenterCoord;
        currCenterCoord = new Vector2(0, 0);

        if (0 < maxTouchCount) //touch Allowed
        {
            if (debugMode)
            {
                for (int i = 0; i < activeTouchCount && i < maxTouchCount; i++)
                {
                    touchpoints[i].SetActive(false);
                }
                centerPoint.SetActive(false);
            }

            prevActiveTouchCount = activeTouchCount;
            activeTouchCount = 0;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (!isAreaLocked || lockedArea.Contains(touch.position))
                {
                    touchList.Add(touch);
                    activeTouchCount = (ushort)Mathf.Min(activeTouchCount + 1, maxTouchCount);
                }
            }
            touches = touchList.ToArray();

            if (prevActiveTouchCount != activeTouchCount)
            {
                deltaScale = 0;
                deltaScaleTotal = 0;
                if (debugMode)
                {
                    Debug.Log("Input.touchCount changed from:"+prevActiveTouchCount.ToString()+" to: " + activeTouchCount.ToString());
                }
            }

            //touch detected
            if (activeTouchCount > 0)
            {
                tabCount = (ushort)touches[0].tapCount;

                if (prevActiveTouchCount == activeTouchCount)
                {
                    for (int i = 0; i < activeTouchCount && i < maxTouchCount; i++)
                    {
                        Touch touch = touches[i];
                        currCenterCoord += touch.position;
                        if (debugMode)
                        {
                            touchpoints[i].SetActive(true);
                            touchpoints[i].transform.position = touch.position;
                            touchpoints[i].transform.Find("index text").GetComponent<Text>().text = touch.fingerId.ToString() ;
                        }
                    }
                    currCenterCoord /= Mathf.Clamp(activeTouchCount, 1, maxTouchCount);

                    if (debugMode)
                    {
                        centerPoint.SetActive(true);
                        Vector3 pos = new Vector3(currCenterCoord.x, currCenterCoord.y, 1);
                        centerPoint.transform.position = pos;
                    }

                    if (prevCenterCoord.magnitude == 0)
                    {
                        prevCenterCoord = currCenterCoord;
                    }
                    deltaPosition = currCenterCoord - prevCenterCoord;
                    deltaPositionTotal += deltaPosition;
                }

                //Multitouch Gesture mode on
                if (activeTouchCount > 1)
                {
                    if (prevActiveTouchCount != activeTouchCount)
                    {
                        prevBorderDelta = 0;
                        borderLength = 0;
                        initialBorderLength = 0;
                    }
                    else
                    {
                        //Scaling
                        borderLength = 0;
                        for (int i = 1; i < activeTouchCount && i < maxTouchCount; i++)
                        {
                            borderLength += Vector2.Distance(touches[i - 1].position, touches[i].position);
                        }

                        prevBorderDelta = borderDelta;
                        if (initialBorderLength != 0)
                        {
                            deltaScaleTotal = borderDelta = borderLength / initialBorderLength;
                            deltaScale = borderDelta - prevBorderDelta;
                        }
                        else
                        {
                            initialBorderLength = borderLength;
                        }

                        //Rotating
                        prevRadianRotation = currRadianRotation;
                        relativeTouch0Position = touches[0].position - currCenterCoord;
                        currRadianRotation = Mathf.Atan2(relativeTouch0Position.y, relativeTouch0Position.x);
                        deltaRotation = currRadianRotation - prevRadianRotation;
                        deltaRotationTotal += deltaRotation;
                    }


                    if (debugMode)
                    {
                        centerPoint.transform.localScale = new Vector3(Mathf.Max(0, 1 * borderDelta), Mathf.Max(0, 1 * borderDelta), 1);
                        Vector3 rotation = centerPoint.transform.rotation.eulerAngles;
                        rotation.z = Mathf.Rad2Deg * deltaRotationTotal;
                        centerPoint.transform.rotation = Quaternion.Euler(rotation);
                    }
                }
                else
                {
                    borderDelta = 1;
                }
            }
            else
            {
                //there is no touches
                if (prevActiveTouchCount != 0)
                {
                    if (debugMode)
                    {
                        centerPoint.transform.localScale = new Vector3(1, 1, 1);
                        centerPoint.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 0));

                        Debug.Log("center moved by: " + deltaPositionTotal.ToString());
                        Debug.Log("rotated by: " + deltaRotationTotal.ToString() + "in rad, "+(Mathf.Rad2Deg * deltaRotationTotal).ToString() +"in deg");
                        Debug.Log("scaled by: " + borderDelta.ToString());
                    }

                    deltaScale       = 0;
                    deltaScaleTotal    = 0;

                    deltaRotation    = 0;
                    deltaRotationTotal = 0;

                    deltaPosition    = new Vector3(0, 0, 0);
                    deltaPositionTotal = new Vector3(0, 0, 0);

                    borderDelta = 1;

                }
                return;
            }

        }
    }
}
