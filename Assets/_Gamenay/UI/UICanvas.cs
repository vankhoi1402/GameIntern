using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

    public class UICanvas : MonoBehaviour
    {
        //public bool IsAvoidBackKey = false;
        public bool IsDestroyOnClose = false;

        protected RectTransform m_RectTransform;
        private Animator m_Animator;

        private void Start()
        {
            OnInit();
        }

        //Init default Canvas
        //khoi tao gia tri canvas
        protected void OnInit()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_Animator = GetComponent<Animator>();
        }

        //Setup canvas to avoid flash UI
        //set up mac dinh cho UI de tranh truong hop bi nhay' hinh
        public virtual void Setup()
        {
            UIManager.Ins.AddBackUI(this);
            UIManager.Ins.PushBackAction(this, BackKey);
        }

        //back key in android device
        //back key danh cho android
        public virtual void BackKey()
        {

        }

        //Open canvas
        //mo canvas
        public virtual void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        //close canvas directly
        //dong truc tiep, ngay lap tuc
        public virtual void CloseDirectly()
        {
            UIManager.Ins.RemoveBackUI(this);
            gameObject.SetActive(false);
            if (IsDestroyOnClose)
            {
                Destroy(gameObject);
            }
        }

        //close canvas with delay time, used to anim UI action
        //dong canvas sau mot khoang thoi gian delay
        public virtual void Close(float delayTime)
        {
            Invoke(nameof(CloseDirectly), delayTime);
        }
    }