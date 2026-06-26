using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LayerLab.SuperCasual
{
    public class PanelSuperCasual : MonoBehaviour
    {
        [SerializeField] private GameObject[] otherPanels;

        public void OnEnable()
        {
            if (otherPanels == null)
                return;

            for (int i = 0; i < otherPanels.Length; i++)
            {
                if (otherPanels[i] != null)
                    otherPanels[i].SetActive(true);
            }
        }

        public void OnDisable()
        {
            if (otherPanels == null)
                return;

            for (int i = 0; i < otherPanels.Length; i++)
            {
                if (otherPanels[i] != null)
                    otherPanels[i].SetActive(false);
            }
        }
    }
}
