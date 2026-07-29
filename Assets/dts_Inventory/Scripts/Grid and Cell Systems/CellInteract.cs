using UnityEngine;
using UnityEngine.EventSystems;


namespace dtsInventory
{
    public class CellInteract : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        //Declarations
        [SerializeField] private (int, int) _index;
        [SerializeField] private InvGrid _grid;
        [SerializeField] private InvItem _item;

        //Monobehaviours






        //Internals




        //Interface
        public void OnPointerEnter(PointerEventData eventData)
        {
            
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            
        }




        //Externals
        public (int, int) Index() { return _index; }
        public InvGrid Grid() { return _grid; }
        public InvItem Item() { return _item; }
        public void SetIndex((int, int) newIndex) { _index = newIndex; }
        public void SetGrid(InvGrid newGrid) { _grid = newGrid; }
        public void SetItem(InvItem item) { _item = item; }


    }
}

