using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



namespace GIF
{
    public class ItemCreator : MonoBehaviour
    {
        [SerializeField] private GameObject _itemBasePrefab;
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private List<ItemData> _itemsList = new();

        private void Awake()
        {
            ItemCreatorHelper.SetItemCreator(this);
        }

        private GameObject FindUnusedItem(ItemData itemData)
        {
            InvItem childItemComponent = null;

            foreach (Transform child in _itemContainer)
            {
                childItemComponent = child.GetComponent<InvItem>();
                if ( childItemComponent != null)
                {
                    if (childItemComponent.ItemData() == itemData)
                    {
                        return child.gameObject;
                    }
                }
            }

            return null;
        }



        public GameObject CreateItem(ItemData itemData, float tileWidth, float tileHeight)
        {
            //ignore the command if we have no items in the list to create.
            if (_itemsList.Count == 0 || _itemBasePrefab == null)
                return null;

            //attempt to recycle an unused item
            GameObject itemObject = FindUnusedItem(itemData);
            bool isItemRecycled = true;

            //create a new item if we didn't find an unused one
            if (itemObject == null)
            {
                itemObject = Instantiate(_itemBasePrefab);
                isItemRecycled = false;
            }

            
            //cache item's references for readability
            InvItem invItem = itemObject.GetComponent<InvItem>();
            RectTransform itemRectTransform = itemObject.GetComponent<RectTransform>();

            //initialize item's data (if its new)
            if (!isItemRecycled)
            {
                itemObject.name = itemData.name;
                invItem.SetItemData(itemData);
                itemObject.GetComponent<Image>().sprite = itemData.Sprite();
            }

            //otherwise, rotate the recycled item back to it's default rotation (if applicable)
            else
            {
                itemObject.SetActive(true);

                int iterationsPerformed = 0;
                int maxIterations = System.Enum.GetNames(typeof(ItemRotation)).Length;

                //keep rotating the item; break if we've performed all the rotations, but still couldn't find the correct matching rotation
                while (invItem.Rotation() != ItemRotation.None && iterationsPerformed < maxIterations)
                {
                    invItem.RotateItem(RotationDirection.Clockwise);
                    iterationsPerformed++;
                }

                //log the error if we couldn't find the rotation
                if (iterationsPerformed >= maxIterations && invItem.Rotation() != ItemRotation.None)
                {
                    Debug.LogWarning($"Attempted to recycle an unused item, but failed to rotate the item to its default rotation. " +
                        $"Loop went through all {maxIterations} possible rotations defined by the enum 'ItemRotation' and failed. Serving" +
                        $" item as is.");
                }
            }

            //resize the item based on the given tile dimensions
            itemRectTransform.sizeDelta = new Vector2(invItem.Width() * tileWidth, invItem.Height() * tileHeight);

            //put the pivot position on the item's specified itemHandle cell position
            Vector2 offsetToCellCenter = new Vector2(tileWidth / 2, tileHeight / 2);
            Vector2 cellHandlePosition = new Vector2(invItem.ItemHandle().Item1 * tileWidth, invItem.ItemHandle().Item2 * tileHeight);
            Vector2 offsetHandlePosition = new Vector2(cellHandlePosition.x + offsetToCellCenter.x, cellHandlePosition.y + offsetToCellCenter.y);

            //calculate the item's size by inferring it through the item's spacialDef indexes
            int xCellMinimum = int.MinValue;
            int yCellMinimum = int.MinValue;
            int xCellMaximum = int.MaxValue;
            int yCellMaximum = int.MinValue;

            bool firstIteration = true;

            foreach ((int, int) index in invItem.GetSpacialDefinition())
            {
                //the first value is both the min and max to start
                if (firstIteration)
                {
                    xCellMinimum = index.Item1;
                    xCellMaximum = index.Item1;
                    yCellMaximum = index.Item2;
                    yCellMinimum = index.Item2;

                    firstIteration = false;
                }
                else
                {
                    if (index.Item1 < xCellMinimum)
                        xCellMinimum = index.Item1;
                    if (index.Item1 > xCellMaximum)
                        xCellMaximum = index.Item1;
                    if (index.Item2 < yCellMinimum)
                        yCellMinimum = index.Item2;
                    if (index.Item2 > yCellMaximum)
                        yCellMaximum = index.Item2;
                }
            }

            //get the total range of cells along each dimension (width and height)
            int xCellCount = xCellMaximum - xCellMinimum + 1; //add 1 to include the origin position)
            int yCellCount = yCellMaximum - yCellMinimum + 1;

            //finally, normalize the previously-calculated offsetHandlePosition by the item's total size
            Vector2 itemSize = new Vector2(xCellCount * tileWidth, yCellCount * tileHeight);
            Vector2 normalizedPivotPosition = new Vector2(offsetHandlePosition.x / itemSize.x, offsetHandlePosition.y / itemSize.y);
            Vector2 normalizedleftBottomMostCellPosition = new Vector2(xCellMinimum * tileWidth / itemSize.x, yCellMinimum * tileHeight /itemSize.y);

            //set the item's pivot point
            itemRectTransform.pivot = normalizedPivotPosition - normalizedleftBottomMostCellPosition;

            //reparent the item to the itemContainer (not an inventory)
            itemRectTransform.SetParent(_itemContainer, false);
            return itemObject;
        }

        public GameObject CreateRandomItem(float tileWidth, float tileHeight)
        {
            if (_itemsList.Count == 0)
                return null;

            int randomIndex = Random.Range(0, _itemsList.Count);
            return CreateItem(_itemsList[randomIndex], tileWidth, tileHeight);
        }

        public HashSet<ItemData> GetItemList()
        {
            HashSet<ItemData> listCopy = new HashSet<ItemData>();

            foreach (ItemData item in listCopy)
                listCopy.Add(item);

            return listCopy;
        }

        public Transform GetItemContainer()
        {
            return _itemContainer;
        }

        public ItemData GetItemDataFromCode(string code)
        {
            foreach (ItemData data in _itemsList)
            {
                if (code == data.ItemCode())
                    return data;
            }

            Debug.LogWarning($"Attempted to find an itemData from itemCode '{code}', but code not found. returning null");
            return null;
        }

        public void ReturnItem(InvItem item)
        {
            if (item == null)
                return;

            item.GetComponent<RectTransform>().SetParent(_itemContainer, false);
            item.gameObject.SetActive(false);
        }
    }

    public static class ItemCreatorHelper
    {
        private static ItemCreator _creator;



        public static void SetItemCreator(ItemCreator creator) { _creator = creator; }

        public static GameObject CreateItem(ItemData itemData, float tileWidth, float tileHeight) 
        { 
            if (_creator != null)
                return _creator.CreateItem(itemData, tileWidth, tileHeight);
            else
            {
                Debug.LogWarning("An active ItemCreator doesn't exist in your scene. Add the Script anywhere in the scene, or ensure its" +
                    " awake before attempting to create any items. Only 1 should exist in a scene.");
                return null;
            }
        }
        public static GameObject CreateRandomItem(float tileWidth, float tileHeight) 
        { 
            if (_creator != null)
                return _creator.CreateRandomItem(tileWidth, tileHeight);
            else
            {
                Debug.LogWarning("An active ItemCreator doesn't exist in your scene. Add the Script anywhere in the scene, or ensure its" +
                    " awake before attempting to create any items. Only 1 should exist in a scene.");
                return null;
            }
        }
        public static Transform GetUiItemsContainer() 
        { 
            if (_creator != null)
                return _creator.GetItemContainer();
            else
            {
                Debug.LogWarning("An active ItemCreator doesn't exist in your scene. Add the Script anywhere in the scene, or ensure its" +
                    " awake before attempting to create fetch any of its references. Only 1 should exist in a scene.");
                return null;
            }
        }
        public static ItemData GetItemDataFromItemCode(string code) 
        { 
            if (_creator != null)
                return _creator.GetItemDataFromCode(code);
            else
            {
                Debug.LogWarning("An active ItemCreator doesn't exist in your scene. Add the Script anywhere in the scene, or ensure its" +
                    " awake before attempting to reverse-lookup items by their itemcode. Only 1 should exist in a scene.");
                return null;
            }
        }
        public static void ReturnItemToCreator(InvItem item) 
        { 
            if (_creator != null)
                _creator.ReturnItem(item);
            else
            {
                Debug.LogWarning("An active ItemCreator doesn't exist in your scene. Add the Script anywhere in the scene, or ensure its" +
                    " awake before attempting to return items to their creator. Only 1 should exist in a scene, and IT SHOULD NOT BE DESTROYED.");
                
            }
        }


    }
}

