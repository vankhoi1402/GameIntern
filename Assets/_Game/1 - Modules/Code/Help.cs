using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

    public static class Help
    {
        public static bool IsPointerOverUI()
        {
#if UNITY_EDITOR
            return EventSystem.current.IsPointerOverGameObject(); // Mouse
#else
    if (Input.touchCount > 0)
        return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
    return false;
#endif
        }

        public static void MoveAlongCurve(Transform targetObject, Vector3 startPosition, Vector3 endPosition, float duration = 1f, float curveHeight = 2f)
        {
            if (targetObject == null)
            {
                Debug.LogWarning("Chưa gán targetObject!");
                return;
            }

            // Tính các điểm giữa để tạo đường cong
            Vector3 direction = (endPosition - startPosition);
            Vector3 midPoint1 = startPosition + direction * 0.25f + Vector3.up * curveHeight * 0.5f; // Điểm giữa 1
            Vector3 midPoint2 = startPosition + direction * 0.5f + Vector3.up * curveHeight;       // Điểm giữa chính (cao nhất)
            Vector3 midPoint3 = startPosition + direction * 0.75f + Vector3.up * curveHeight * 0.5f; // Điểm giữa 3

            // Tạo mảng các điểm cho đường dẫn
            Vector3[] pathPoints = new Vector3[]
            {
        startPosition,  // Điểm bắt đầu
        midPoint1,      // Điểm giữa 1
        midPoint2,      // Điểm giữa chính (cao nhất)
        midPoint3,      // Điểm giữa 3
        endPosition     // Điểm kết thúcs
            };

            // Đặt vị trí ban đầu của object
            targetObject.localPosition = startPosition;

            // Tạo tween với DOPath
            Tween tween = targetObject.DOLocalPath(pathPoints, duration, PathType.Linear, PathMode.Full3D, 10, Color.green)
                                      .SetEase(Ease.Linear)
                                      .SetTarget(targetObject);
        }

        public static Vector3 GetRandomPosition()
        {
            Camera mainCamera = Camera.main;
            float screenZ = 0f; // You can adjust the Z position as needed

            // Get the screen boundaries in world space
            Vector3 bottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, screenZ));
            Vector3 topRight = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, screenZ));

            // Generate random x and y within the boundaries
            float randomX = Random.Range(bottomLeft.x + 0.5f, topRight.x - 0.5f);
            float randomY = Random.Range(bottomLeft.y + 1, topRight.y - 1);

            // Return the random position
            return new Vector3(randomX, randomY, screenZ);
        }

        public static Vector3 GetBorderVector()
        {
            Vector3 screenBorderWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0));
            return screenBorderWorldPos;
        }

        public static Vector3 BoderLeft()
        {
            Vector3 screenBorderWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0));
            return new Vector3(-screenBorderWorldPos.x + 0.5f, 0, 0);
        }

        public static Vector3 BorderRight()
        {
            Vector3 screenBorderWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0));
            return new Vector3(screenBorderWorldPos.x - 0.5f, 0, 0);
        }


        public static Vector2 BottomLeft() => new Vector2(-Screen.width / 2f, -Screen.height / 2f);
        public static Vector2 TopLeft() => new Vector2(-Screen.width, Screen.height);
        public static Vector2 MiddleBottom() => new Vector2(0, -Screen.height / 2f);

        public static Vector3 Random100() => new Vector3(Random.Range(-100, 100), Random.Range(-100, 100));

        public static Vector3 GetWorldMousePosition()
        {
            Vector3 mousePoint = Input.mousePosition;
            mousePoint.z = Camera.main.nearClipPlane;
            return Camera.main.ScreenToWorldPoint(mousePoint);
        }




        public static Vector3 ConvertUIToWorldPosition(RectTransform uiElement)
        {
            // Get the screen position of the UI element
            Vector3 screenPosition = RectTransformUtility.WorldToScreenPoint(Camera.main, uiElement.position);

            // Convert the screen position to world position
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Camera.main.nearClipPlane));

            return worldPosition;
        }

        public static Vector3 ConvertWorldToUIPosition(Vector3 worldPosition, RectTransform canvasRect)
        {
            // Convert world position to screen position
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

            // Convert screen position to canvas position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvasRect.GetComponentInParent<Canvas>().worldCamera,
                out Vector2 uiPosition
            );

            return uiPosition;
        }
        // public static Vector3 ConvertUIPositionToUIPosition(RectTransform uiElement1, Canvas canvas1, Canvas canvas2)
        // {
        //     Vector3 position = new Vector3();

        //     Vector3 worldPosition = ConvertUIToWorldPosition(uiElement1);
        //     position = ConvertWorldToUIPosition(worldPosition, GameManager.Ins.MainCanvasRect);

        //     return position;
        // }

        public static void SetAsSecondLastSibling(Transform tartget)
        {
            tartget.SetSiblingIndex(tartget.parent.childCount - 2);
        }
        public static void SetAsThirdLastSibling(Transform tartget)
        {
            tartget.SetSiblingIndex(tartget.parent.childCount - 3);
        }

        public static void ClearDebug()
        {
            #if UNITY_EDITOR
            // Cách hiện đại và ổn định nhất (Unity 2018 → Unity 6)
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            
            if (method != null)
                method.Invoke(null, null);
            else
                Debug.LogWarning("Không tìm thấy hàm Clear Console");
            #endif
        }

        public static IEnumerator CaptureScreen(System.Action<Sprite> onComplete)
        {
            // Wait for the end of the frame to ensure rendering is complete
            yield return new WaitForEndOfFrame();

            int width = Screen.width;
            int height = Screen.height;

            // Create a new Texture2D with screen dimensions
            Texture2D screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Read the pixels from the screen
            screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenTexture.Apply();

            // Convert the Texture2D to a Sprite
            Sprite screenSprite = Sprite.Create(screenTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));

            // Execute the callback with the captured sprite
            onComplete?.Invoke(screenSprite);

            Debug.Log("Screenshot captured and converted to Sprite!");
        }

        public static string FormatBigCount(int count)
        {
            if (count > 9_000_000)
                return "9.99M";

            if (count < 1000)
            {
                return count.ToString();
            }
            else if (count < 1_000_000)
            {
                float formattedCount = count / 1000f;
                return formattedCount.ToString("0.##") + "k";
            }
            else
            {
                float formattedCount = count / 1_000_000f;
                return formattedCount.ToString("0.##") + "M";
            }
        }
        public static string FormatTimeDisplay(int seconds)
        {
            if (seconds <= 0)
                return "0s";

            // Tính giờ, phút, giây
            int hours = seconds / 3600;
            int remainingSeconds = seconds % 3600;
            int minutes = remainingSeconds / 60;
            int secs = remainingSeconds % 60;

            // Hiển thị kết hợp giờ + phút
            if (hours > 0 && minutes > 0)
                return $"{hours}h{minutes}m";

            // Hiển thị chỉ giờ
            if (hours > 0)
                return $"{hours}h";

            // Hiển thị chỉ phút
            if (minutes > 0)
                return $"{minutes}m";

            // Hiển thị chỉ giây
            return $"{secs}s";
        }

        public static string FormatBigCount2(int count)
        {
            if (count > 9_000_000)
                return "9.99M";

            if (count < 1000)
            {
                return count.ToString();
            }
            else if (count < 1_000_000)
            {
                int formattedCount = (int)(count / 1000f);
                return formattedCount.ToString() + "k";
            }
            else
            {
                int formattedCount = (int)(count / 1_000_000f);
                return formattedCount.ToString() + "M";
            }
        }

        public static bool CheckInternetConnection()
        {
            bool hasInternet = false;
            switch (Application.internetReachability)
            {
                case NetworkReachability.ReachableViaCarrierDataNetwork:
                    hasInternet = true;
                    Debug.Log("Có kết nối internet qua dữ liệu di động.");
                    break;

                case NetworkReachability.ReachableViaLocalAreaNetwork:
                    hasInternet = true;
                    Debug.Log("Có kết nối internet qua Wi-Fi.");
                    break;

                case NetworkReachability.NotReachable:
                    hasInternet = false;
                    Debug.Log("Không có kết nối internet.");
                    break;
            }

            return hasInternet;
        }

        public static string ConvertHightString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input; // Trả về chuỗi rỗng nếu input không hợp lệ
            }

            string result = input[0].ToString(); // Bắt đầu với ký tự đầu tiên

            // Duyệt từ ký tự thứ 2 (index 1) đến hết chuỗi
            for (int i = 1; i < input.Length; i++)
            {
                // Nếu ký tự hiện tại là chữ in hoa, thêm khoảng trắng trước nó
                if (char.IsUpper(input[i]) || char.IsDigit(input[i]))
                {
                    result += " ";
                }
                result += input[i];
            }

            return result;
        }

        public static bool IsPrefab(GameObject go)
        {
#if UNITY_EDITOR
            // Works in editor only
            return UnityEditor.PrefabUtility.GetPrefabAssetType(go) != UnityEditor.PrefabAssetType.NotAPrefab
                   && !go.scene.IsValid();
#else
        // In builds, check scene validity
        return !go.scene.IsValid();
#endif
        }
    }