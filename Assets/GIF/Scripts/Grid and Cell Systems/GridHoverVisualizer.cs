using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GIF
{
    public class GridHoverVisualizer : MonoBehaviour
    {
        //declarations
        [Header("References")]
        [SerializeField] private GameObject _hoverObjectPrefab;
        [SerializeField] private RectTransform _inactiveHoverContainer;
        [SerializeField] private RectTransform _hoverOverlayContainer;
        [SerializeField] private InvGrid _grid;
        private Dictionary<(int, int), GameObject> _activeHoverObjects = new();





        //monobehaviours




        //internals
        



        //externals
        public void CreateHoverOnCell((int, int) cell)
        {
            if (_hoverObjectPrefab == null)
            {
                Debug.LogWarning("Can't create a hover effect object if the prefab is null. Ignoring hover effect request.");
                return;
            }

            if (_hoverOverlayContainer == null)
            {
                Debug.LogWarning("Can't create a hover effect object without specifying the overlay container. Ignoring hover effect request.");
                return;
            }

            if (_grid == null)
            {
                Debug.LogWarning("Can't create a hover effect object if the GRID is null. Ensure this component is attached to an object with an InvGrid component. " +
                    " Ignoring hover effect request.");
                return;
            }

            if (!_grid.IsCellOnGrid(cell))
            {
                Debug.LogWarning($"Attempted to create a hover effect outside of grid space ({cell.Item1},{cell.Item2}). Ignoring hover effect request.");
                return;
            }

            if (_inactiveHoverContainer == null)
            {
                Debug.LogWarning("'Inactive Hover Objects' container is null. Make sure this is set to enable object recycling. Ignoring hover effect request.");
                return;
            }


            if (_activeHoverObjects.ContainsKey(cell))
            {
                Debug.LogWarning($"Hover obeject already exists at position ({cell.Item1},{cell.Item2})");
                return;
            }

            GameObject newHoverEffect;
            RectTransform newHoverRectTransform;

            //Debug.Log($"container position before hover creation: {_hoverOverlayContainer.name} => {_hoverOverlayContainer.position}");

            if (_inactiveHoverContainer.childCount == 0)
            {
                newHoverEffect = Instantiate(_hoverObjectPrefab, _inactiveHoverContainer);
                newHoverRectTransform = newHoverEffect.GetComponent<RectTransform>();
            }
            else
            {
                newHoverEffect = _inactiveHoverContainer.GetChild(0).gameObject;
                newHoverRectTransform = newHoverEffect.GetComponent<RectTransform>();
            }
            newHoverEffect.SetActive(true);
            _activeHoverObjects.Add(cell, newHoverEffect);

            //Debug.Log($"container position before overlaying hover effect on grid: {_hoverOverlayContainer.name} => {_hoverOverlayContainer.position}");

            _grid.OverlayRectTransformOntoGrid(newHoverRectTransform,_hoverOverlayContainer,cell,true);

            //Debug.Log($"container position AFTER hover creation: {_hoverOverlayContainer.name} => {_hoverOverlayContainer.position}");
            //Debug.Log($"created hover effect on cell ({cell.Item1},{cell.Item2})");

        }
        public void ClearCellHover((int,int) cell)
        {
            if (_activeHoverObjects.ContainsKey(cell))
            {
                GameObject existingCell = _activeHoverObjects[cell];
                existingCell.SetActive(false);
                _activeHoverObjects.Remove(cell);

                existingCell.GetComponent<RectTransform>().SetParent(_inactiveHoverContainer);
                //Debug.Log($"removed hover effect from cell ({cell.Item1},{cell.Item2})");
            }
        }

        public void ClearAllHoveredCells()
        {
            List<GameObject> activeHoverEffects = _activeHoverObjects.Values.ToList();
            _activeHoverObjects.Clear();

            foreach (GameObject hoverEffect in activeHoverEffects)
            {
                hoverEffect.SetActive(false);
                hoverEffect.GetComponent<RectTransform>().SetParent(_inactiveHoverContainer);
            }
        }






    }
}
