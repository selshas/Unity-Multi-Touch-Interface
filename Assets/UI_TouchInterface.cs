using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.IO;

public class UI_TouchInterface : MonoBehaviour {

    public bool isAreaLocked = false;
    private Rect lockedArea;

    private Canvas canvas;
    private Rect canvasRect;
    public Camera mainCam;

    public Vector2 currCenterCoord = new Vector2(0, 0);
    public Vector2 prevCenterCoord = new Vector2(0, 0);

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

    public ushort maxTouchCount = 5;      //maximum limit of touch count.
    private Touch[] touches;

    private Vector3 relativeTouch0Position;

    private ushort prevActiveTouchCount = 0;
    private ushort activeTouchCount = 0;

    //Debug tools
    public bool debugMode = false;
    private GameObject[] touchpoints;
    private GameObject centerPoint;
    public Material mat;

    private struct LockingInformation
    {
        public Vector2 position;
        public Vector2 size;
    };

    private LockingInformation LockingInfo {
        get {
            LockingInformation result = new LockingInformation();

            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            Vector2 canvasPosition = canvas.transform.position;

            Vector2 position = new Vector2();
            position = canvasPosition + new Vector2(rectTransform.localPosition.x, rectTransform.localPosition.y) + new Vector2(rectTransform.rect.x, rectTransform.rect.y);
            
            result.position = position;
            result.size = new Vector2(rectTransform.rect.xMax - rectTransform.rect.xMin, rectTransform.rect.yMax - rectTransform.rect.yMin);

            return result;
        }
    }

    // Use this for initialization
    private void Awake()
    {
        touches     = new Touch[maxTouchCount];
        touchpoints = new GameObject[maxTouchCount];

        canvas = gameObject.GetComponentInParent<Canvas>();
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        canvasRect = canvas.gameObject.GetComponent<RectTransform>().rect;

        //create touch point visualization objects, if debugMode is true;
        if (debugMode)
        {
            //report initial locking area
            LockingInformation lockingInfo = LockingInfo;
            Debug.Log("Locking Start from: " + lockingInfo.position.ToString());
            Debug.Log("Locking End at: " + (lockingInfo.position + lockingInfo.size).ToString());

            //add center point visualizations
            centerPoint = new GameObject("touch center Point");

            centerPoint.AddComponent<Image>();
            Image tcp_img = centerPoint.GetComponent<Image>();
            tcp_img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            tcp_img.color = new Color32(255,0,0,255);
            tcp_img.raycastTarget = false;

            RectTransform tcp_transform = centerPoint.GetComponent<RectTransform>();
            centerPoint.transform.SetParent(canvas.transform);
            centerPoint.transform.localScale = new Vector3(1, 1, 1);
            tcp_transform.anchorMax = new Vector2(0, 0);
            tcp_transform.anchorMin = new Vector2(0, 0);

            //add touch point visualizations
            for (int i = 0; i < maxTouchCount; i++)
            {
                GameObject touchpoint = new GameObject("touch point"+i.ToString());
                touchpoint.AddComponent<Image>();
                Image tp_img = touchpoint.GetComponent<Image>();
                tp_img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                tp_img.raycastTarget = false;

                GameObject touchPointIndex = new GameObject("index text");
                touchPointIndex.AddComponent<Text>();
                Text tp_text = touchPointIndex.GetComponent<Text>();
                tp_text.text = i.ToString();
                tp_text.alignment = TextAnchor.MiddleCenter;
                tp_text.fontSize = 24;
                tp_text.fontStyle = FontStyle.Bold;
                tp_text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                tp_text.raycastTarget = false;
                touchPointIndex.transform.localPosition = new Vector3(0, 80.0f, 0);
                touchPointIndex.transform.localScale = new Vector3(1, 1, 1);
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
        if (debugMode)
        {
            Image image = gameObject.GetComponent<Image>();
            if (gameObject.GetComponent<Image>() == null)
            {
                gameObject.AddComponent<Image>();
                image = gameObject.GetComponent<Image>();
                image.color = new Color(1, 1, 1, 0.2f);
                image.raycastTarget = false;
            }
            else
            {
                image.color = new Color(1,1,1, 0.2f);
            }
        }
        else
        {
            Image image = gameObject.GetComponent<Image>();
            if (image != null)
                Destroy(image);
        }

        List<Touch> touchList = new List<Touch>();

        //get a Rect instance, represents touch area on the screen.
        if (isAreaLocked)
        {
            LockingInformation lockingInfo = LockingInfo;
            lockedArea = new Rect(lockingInfo.position, lockingInfo.size);
        }

        //save previous CenterCoord, initialize new CenterCoord
        prevCenterCoord = currCenterCoord;
        currCenterCoord = new Vector2(0, 0);

        if (0 < maxTouchCount) //touch Allowed
        {
            //reset active state to unvisualize.
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
                Vector2 pos = touch.position;
                pos.x = canvas.transform.position.x + (pos.x - (canvasRect.width / 2));
                pos.y = canvas.transform.position.y + (pos.y - (canvasRect.height / 2));
                
                if (!isAreaLocked || lockedArea.Contains(pos))
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

                if (prevActiveTouchCount == 0) {
                    //touch fired.
                }
                else if (prevActiveTouchCount < activeTouchCount)
                {
                    //touch count increased.
                }
                else if (activeTouchCount < prevActiveTouchCount)
                {
                    //touch count decreased.
                }
                else if (prevActiveTouchCount == activeTouchCount)
                {
                    //touch count stational
                    for (int i = 0; i < activeTouchCount && i < maxTouchCount; i++)
                    {
                        Touch touch = touches[i];
                        currCenterCoord += touch.position;
                        if (debugMode)
                        {
                            Vector2 pos = touch.position;
                            pos.x -= (canvasRect.width / 2);
                            pos.y -= (canvasRect.height / 2);

                            touchpoints[i].SetActive(true);
                            touchpoints[i].transform.localPosition = pos;
                            touchpoints[i].transform.localScale = new Vector3(1,1,1);
                            touchpoints[i].transform.Find("index text").GetComponent<Text>().text = touch.fingerId.ToString();
                        }
                    }
                    currCenterCoord /= Mathf.Clamp(activeTouchCount, 1, maxTouchCount);

                    if (debugMode)
                    {
                        centerPoint.SetActive(true);
                        Vector3 pos = new Vector3(currCenterCoord.x, currCenterCoord.y, 1);
                        pos.x -= (canvasRect.width / 2);
                        pos.y -= (canvasRect.height / 2);
                        centerPoint.transform.localPosition = pos;
                    }

                    if (prevCenterCoord.magnitude == 0)
                    {
                        prevCenterCoord = currCenterCoord;
                    }
                    deltaPosition = currCenterCoord - prevCenterCoord;
                    deltaPositionTotal += deltaPosition;
                }

                //raycast
                
                GraphicRaycaster raycaster = canvas.gameObject.GetComponent<GraphicRaycaster>();
                for (int i = 0; i < activeTouchCount; i++)
                {
                    Ray ray = mainCam.ScreenPointToRay(touches[i].position);

                    PointerEventData eventData = new PointerEventData(null);
                    eventData.position = touches[i].position;

                    List<RaycastResult> result = new List<RaycastResult>();
                    raycaster.Raycast(eventData, result);

                    if (result.Count > 0)
                    {
                        result.Sort(delegate (RaycastResult a, RaycastResult b)
                        {
                            float az = a.gameObject.transform.position.z;
                            float bz = b.gameObject.transform.position.z;
                            if (az < bz)
                            {
                                return -1;
                            }
                            else if (az > bz)
                            {
                                return 1;
                            }
                            return 0;
                        });

                        Debug.Log(result[0].gameObject.name);
                    }
                }
               

                //Multi-touch Gesture mode on
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
