using System.Collections.Generic;
using UnityEngine;


namespace dtsInventory
{
    public enum ItemRotation
    {
        None, //0 or 360
        Once, //90
        Twice, //180
        Thrice, //270
    }

    public enum RotationDirection
    {
        Clockwise,
        CounterClockwise
    }


    public class InvItem : MonoBehaviour
    {
        //Declarations
        [SerializeField] ItemData _itemData;
        [SerializeField] (int, int) _itemHandle;
        [SerializeField] HashSet<(int, int)> _spacialDefinition = new();
        [SerializeField] private ItemRotation _rotation = ItemRotation.None;
        [SerializeField] private Vector2Int _size;
        private RectTransform _rectTransform;



        private void RotateIndexesClockwise()
        {
            HashSet<(int, int)> newIndexes = new();
            foreach ((int, int) index in _spacialDefinition)
            {
                (int, int) newIndex = (index.Item2, -index.Item1);
                newIndexes.Add(newIndex);

                //update the item Handle, too
                if (index == _itemHandle)
                    _itemHandle = newIndex;
            }

            _spacialDefinition = newIndexes;

        }

        private void RotateIndexesCounterClockwise()
        {
            HashSet<(int, int)> newIndexes = new();
            foreach ((int, int) index in _spacialDefinition)
            {
                (int, int) newIndex = (-index.Item2, index.Item1);
                newIndexes.Add(newIndex);

                //update the item handle, too
                if (index == _itemHandle)
                    _itemHandle = newIndex;
            }

            _spacialDefinition = newIndexes;
        }





        public ItemData ItemData() { return _itemData; }
        public void SetItemData(ItemData newItemData)
        {
            _itemData = newItemData;
            _itemHandle = newItemData.ItemHandle();
            _spacialDefinition = newItemData.SpacialDefinition();
            _rectTransform = GetComponent<RectTransform>();

            int xMinIndex=0;
            int yMinIndex=0;
            int xMaxIndex=0;
            int yMaxIndex=0;
            bool firstIteration = true;

            //find the largest and smallest x/y indexes
            foreach ((int, int) index in _spacialDefinition)
            {
                if (firstIteration)
                {
                    xMinIndex = index.Item1;
                    xMaxIndex = index.Item1;
                    yMinIndex = index.Item2;
                    yMaxIndex = index.Item2;
                    firstIteration = false;
                }
                else
                {
                    if (index.Item1 < xMinIndex)
                        xMinIndex = index.Item1;
                    if (index.Item1 > xMaxIndex)
                        xMaxIndex = index.Item1;
                    if (index.Item2 < yMinIndex)
                        yMinIndex = index.Item2;
                    if (index.Item2 > yMaxIndex)
                        yMaxIndex = index.Item2;
                }
                
            }

            //take the differences between the largest and smallest x/y (and include the starting number)
            //this yields the total size of the item
            _size = new Vector2Int(xMaxIndex - xMinIndex + 1, yMaxIndex - yMinIndex + 1);

        }
        public string ItemCode() { return _itemData.ItemCode(); }


        public ItemRotation Rotation() { return _rotation; }
        public (int, int) ItemHandle() { return _itemHandle; }
        public void RotateItem(RotationDirection direction)
        {
            //default the direction to clockwise if given a werid value
            if (direction != RotationDirection.Clockwise && direction != RotationDirection.CounterClockwise)
                direction = RotationDirection.Clockwise;

            //change the indexes depending on the given direction
            if (direction == RotationDirection.Clockwise)
                RotateIndexesClockwise();
            else
            {
                RotateIndexesCounterClockwise();
            }


            //update the rotation state (used to determine sprite rotation)
            switch (_rotation)
            {
                case ItemRotation.None:
                    if (direction == RotationDirection.Clockwise)
                        _rotation = ItemRotation.Once;
                    else _rotation = ItemRotation.Thrice;
                    break;

                case ItemRotation.Once:
                    if (direction == RotationDirection.Clockwise)
                        _rotation = ItemRotation.Twice;
                    else _rotation = ItemRotation.None;
                    break;

                case ItemRotation.Twice:
                    if (direction == RotationDirection.Clockwise)
                        _rotation = ItemRotation.Thrice;
                    else _rotation = ItemRotation.Once;
                    break;

                case ItemRotation.Thrice:
                    if (direction == RotationDirection.Clockwise)
                        _rotation = ItemRotation.None;
                    else _rotation = ItemRotation.Twice;
                    break;

                default:
                    break;
            }

            //reflect the transforms rotation to match the angle
            _rectTransform.rotation = RotationAngle();

        }

        public Quaternion RotationAngle()
        {
            switch (_rotation)
            {
                case ItemRotation.None:
                    return Quaternion.Euler(0, 0, 0);

                case ItemRotation.Once:
                    return Quaternion.Euler(0, 0, -90);

                case ItemRotation.Twice:
                    return Quaternion.Euler(0, 0, -180);

                case ItemRotation.Thrice:
                    return Quaternion.Euler(0, 0, -270);

                default:
                    return Quaternion.Euler(0, 0, 0);
            }
        }

        public HashSet<(int, int)> GetSpacialDefinition() { return _spacialDefinition; }

        public int Width() { return _size.x; }
        public int Height() { return _size.y; }



    }
}
