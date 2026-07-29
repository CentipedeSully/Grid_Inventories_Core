using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



namespace dtsInventory
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

        public GameObject CreateItem(ItemData itemData, float tileWidth, float tileHeight)
        {
            //ignore the command if we have no items in the list to create.
            if (_itemsList.Count == 0 || _itemBasePrefab == null)
                return null;

            //cache item's references for readability
            GameObject itemObject = Instantiate(_itemBasePrefab);
            InvItem invItem = itemObject.GetComponent<InvItem>();
            RectTransform itemRectTransform = itemObject.GetComponent<RectTransform>();

            //initialize item's data
            itemObject.name = itemData.name;
            invItem.SetItemData(itemData);
            itemObject.GetComponent<Image>().sprite = itemData.Sprite();
            itemRectTransform.sizeDelta = new Vector2(invItem.Width()*tileWidth, invItem.Height()*tileHeight);

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

        public static GameObject CreateItem(ItemData itemData, float tileWidth, float tileHeight) { return _creator.CreateItem(itemData, tileWidth, tileHeight); }
        public static GameObject CreateRandomItem(float tileWidth, float tileHeight) { return _creator.CreateRandomItem(tileWidth, tileHeight); }
        public static Transform GetUiItemsContainer() { return _creator.GetItemContainer(); }
        public static ItemData GetItemDataFromItemCode(string code) { return _creator.GetItemDataFromCode(code); }
        public static void ReturnItemToCreator(InvItem item) { _creator.ReturnItem(item); }


    }
}

