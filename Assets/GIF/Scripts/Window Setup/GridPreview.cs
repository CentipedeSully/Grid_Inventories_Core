using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GIF
{
    public class GridPreviewVisualizer : MonoBehaviour
    {
        //[SerializeField] private Vector2 _maxContainerSize = Vector2.one;
        [SerializeField] private InvGrid _invGrid;
        [SerializeField] private Image _gridPotentialArea;
        [SerializeField] private Image _gridBorder;








        [ContextMenu("Update Grid Preview")]
        public void ResizeToMaxSize()
        {
            //resize
            if (_invGrid == null)
            {
                Debug.LogWarning("Attempted to build a preview for an null invGrid. Ensure the reference isn't empty");
                return;
            }
            if (GetComponent<RectTransform>() == null)
            {
                Debug.LogWarning($"To build a grid preview on this gameObject {gameObject.name}, a rectTransform component is required.");
                return;
            }

            Vector2 dynamicSize = new();
            GridLayoutGroup layoutGroup = _invGrid.GetComponent<GridLayoutGroup>();
            dynamicSize.x = _invGrid.ContainerSize().x * _invGrid.CellSize().x + layoutGroup.padding.right + layoutGroup.padding.left;
            dynamicSize.y = _invGrid.ContainerSize().y * _invGrid.CellSize().y + layoutGroup.padding.bottom + layoutGroup.padding.top;

            GetComponent<RectTransform>().sizeDelta = dynamicSize;


            RectTransform gridPotentialArea = _gridPotentialArea.GetComponent<RectTransform>();
            Vector2 gridPotentialSize = new();
            gridPotentialSize.x = dynamicSize.x - (layoutGroup.padding.right + layoutGroup.padding.left);
            gridPotentialSize.y = dynamicSize.y - (layoutGroup.padding.bottom + layoutGroup.padding.top);

            gridPotentialArea.sizeDelta = gridPotentialSize;
        }

    }
}

