using UnityEngine;
using UnityEngine.EventSystems;

namespace GIF
{
    public class GridInteract : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        //Declarations
        private InvGrid _grid;




        //Monobehaviours
        private void Awake()
        {
            _grid = GetComponent<InvGrid>();
        }


        //interface implementations
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_grid != null)
            {
                InteracterHelper.SetGridAsHovered(_grid);
            }

        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_grid != null)
            {
                InteracterHelper.ClearGrid();
            }

        }
    }
}
