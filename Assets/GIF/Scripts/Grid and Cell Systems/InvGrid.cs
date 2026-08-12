using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static UnityEditor.Progress;


namespace GIF
{

    public enum InvOperation
    {
        None,
        Add,
        Remove
    }

    [Serializable]
    public struct InvContentsUpdate
    {
        public InvOperation operation;
        public ItemData itemData;
        public int amount;

        //every element of this list is a stack that was modified.
        //The item that was modified may or may not exist anymore. This is just telling where the actions occurred.
        //Used for updating externals watching the grid. ex: used by the native 'GridInteracter' to update itself when an action changes the grid its hovering over.

        public List<HashSet<(int, int)>> stackAreasAffected; 


        public InvContentsUpdate(ItemData itemTypechanged, int changeAmount, InvOperation operationThatHappened, List<HashSet<(int,int)>> stackAreasChanged)
        {
            this.operation = operationThatHappened;
            this.itemData = itemTypechanged;
            this.amount = changeAmount;

            this.stackAreasAffected = new();

            foreach (HashSet<(int,int)> element in stackAreasChanged)
                stackAreasAffected.Add(element.ToHashSet());
        }

        public InvContentsUpdate(SerializableInvContentsUpdate serializableUpdate)
        {
            this.operation = serializableUpdate.operation;
            this.itemData = serializableUpdate.itemData;
            this.amount = serializableUpdate.amount;

            this.stackAreasAffected = new();

            foreach (InnerList element in serializableUpdate.stackAreasAffected)
            {
                HashSet<(int, int)> stackArea = new();

                foreach (Vector2Int position in element.list)
                    stackArea.Add((position.x, position.y));

                stackAreasAffected.Add(stackArea);
            }
        }
    }

    public class InvGrid : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vector2Int _containerSize;
        [SerializeField] private Vector2 _cellSize;
        private IEnumerator _textUpdater;

        [Header("References")]
        [SerializeField] private GridHoverVisualizer _hoverVisualizer;
        [SerializeField] private GameObject _cellPrefab;
        [Space(20)]
        [SerializeField] private RectTransform _spritesContainer;
        [SerializeField] private RectTransform _stackTextUiPrefab;
        [SerializeField] private RectTransform _activeStackTextsContainer;
        [SerializeField] private RectTransform _unusedStackTextsContainer;
        [SerializeField] private RectTransform _overlayContainer;
        [SerializeField] private List<RectTransform> _otherContainers = new();

        GridLayoutGroup _layoutGroup;




        //events
        public delegate void InvContentsChangedEvent(InvContentsUpdate update);
        public delegate void BulkInvContentsChangedEvent(List<InvContentsUpdate> updates);
        public delegate void InvGridEvent();
        public delegate void InvGridResizedEvent(Vector2 newDimensions);
        public event InvContentsChangedEvent OnContentsChanged;
        public event BulkInvContentsChangedEvent OnBulkContentsChanged;
        public event InvGridEvent OnGridDestroyed;
        public event InvGridResizedEvent OnGridResized;



        [Header("Debug")]
        [Tooltip("Toggles whether or not to listen for debug commands")]
        [SerializeField] private bool _isDebugActive = false;



        [Space(20)]
        [Tooltip("The specified target ItemData. Use depends on the command context.")]
        [SerializeField] private ItemData _paramItemData;

        [Tooltip("The specified value. Use depends on the command context.")]
        [SerializeField] private int _paramValue = 0;

        [Tooltip("The specified position. Used exclusively by the command 'QueryPosition'.")]
        [SerializeField] private Vector2Int _paramPosition= new();


        [Space(20)]
        [Tooltip("Adds x items to the grid. The number of items to add is defined by [Value]. The item to add is defined by [ItemData].")]
        [SerializeField] private bool _cmdAddItem = false;

        [Tooltip("Removes x items from the grid. The number of items to remove is defined by [Value]. The item to remove is defined by [ItemData].")]
        [SerializeField] private bool _cmdRemoveItem = false;

        [Tooltip("Counts how many of a specified item exist within the grid. The specified item is defined by [ItemData].")]
        [SerializeField] private bool _cmdCountItem = false;



        [Space(20)]
        [Tooltip("Logs whatever is at the specified position. The specified position is defined by [Position].")]
        [SerializeField] private bool _cmdQueryPosition = false;

        [Tooltip("When attempting to find space for an item, what positions would you like to mark as 'unavailable-for-placement'? Used exclusively by the command 'FindSpace'.\n\n" +
            "(Advanced Note: This setting exists to help debug the grid's spaceFinding algorithm. If this test fails-- meaning positions aren't accurately being ignored when they should be, " +
            "then the core spaceFinding method has become compromised.)")]
        [SerializeField] private List<Vector2Int> _settingExcludePositionsList = new List<Vector2Int>();

        [Tooltip("Attempts to find a valid placement position ANYWHERE within the grid for a given item. The given item is defined by [ItemData].")]
        [SerializeField] private bool _cmdFindSpaceForItem = false;

        [Tooltip("The list of Querys that you'd like to perform. Used exclusively by the command 'FindSpaceForAll'.")]
        [SerializeField] private List<ItemQuery> _paramItemQueryList;

        [Tooltip("Looks at a collection of 'ItemQueries' and attempts to find space for EVERY ITEM QUERY in the query collection. The itemQuery collection is defined by [ItemQueryList].")]
        [SerializeField] private bool _cmdFindSpaceForAll = false;



        [Space(20)]
        [Tooltip("The number of rows & columns to add/remove from the grid. Used exclusively by the commands 'ExpandGrid' and 'ReduceGrid'.")]
        [SerializeField] private Vector2Int _paramColumnRowAdjustment;

        [Tooltip("Increases the width and height of the grid by specific values. The specific values are defined by [ColumnRowAdjustment].")]
        [SerializeField] private bool _cmdExpandGrid = false;

        [Tooltip("Decreases the width and height of the grid by specific values. The specific values are defined by [ColumnRowAdjustment].")]
        [SerializeField] private bool _cmdReduceGrid = false;

        [Tooltip("Resizes the Grid to match a set size. The specific value is defined by [ColumnRowAdjustment].")]
        [SerializeField] private bool _cmdResizeGrid = false;



        [Header("Unity Events")]
        



        private RectTransform _rectTransform;
        private Dictionary<(int,int),CellInteract> _cellInteractCollection = new();
        //private Dictionary<InventoryItem, List<(int, int)>> _containedItems = new(); //used for quick referencing any item in the grid
        //private Dictionary<(int, int), InventoryItem> _cellOccupancy = new(); //used to quickly check if specific cells are occupied (& what occupies them)

        /// <summary>
        /// The current size of each stack. The keys are the stack's occupied gridPositions.
        /// </summary>
        private Dictionary<HashSet<(int, int)>, int> _stackCapacities = new Dictionary<HashSet<(int, int)>, int>(HashSet<(int, int)>.CreateSetComparer());
        /// <summary>
        /// The itemData values that belong to each stack. The keys are the stack's occupied gridPositions.
        /// </summary>
        private Dictionary<HashSet<(int, int)>, ItemData> _stackItemDatas = new Dictionary<HashSet<(int, int)>, ItemData>(HashSet<(int, int)>.CreateSetComparer());
        /// <summary>
        /// The item graphic that visually defines the stack in the inventory window. The keys are the stack's occupied gridPositions.
        /// </summary>
        private Dictionary<HashSet<(int, int)>, InvItem> _stackSpriteObjects = new Dictionary<HashSet<(int, int)>, InvItem>(HashSet<(int, int)>.CreateSetComparer());
        private Dictionary<HashSet<(int, int)>, TextMeshProUGUI> _stackTexts = new Dictionary<HashSet<(int, int)>, TextMeshProUGUI>(HashSet<(int, int)>.CreateSetComparer());

        /// <summary>
        /// Holds a placement position and a 'rotation' value
        /// </summary>
        public struct ItemPlacementData
        {
            public (int, int) gridPlacementPosition;
            public ItemRotation necessaryRotation;

            public ItemPlacementData((int,int) gridPosition, ItemRotation itemRotation)
            {
                gridPlacementPosition = gridPosition;
                necessaryRotation = itemRotation;
            }
        }

        public struct ItemQueryResponse
        {
            public ItemData itemData;
            public (int,int) placementPosition;
            public ItemRotation placementRotation;
            public HashSet<(int, int)> reservedPositions;
            public int availableCapacity;

            public ItemQueryResponse(ItemData data, (int,int) targetPosition, int capacity,HashSet<(int,int)> fullStackPosition, ItemRotation necessaryRotation)
            {
                itemData = data;
                placementPosition = targetPosition;
                placementRotation = necessaryRotation;
                availableCapacity = capacity;

                reservedPositions = new();
                foreach ((int,int) position in fullStackPosition)
                    reservedPositions.Add(position);
            }
        }

        [Serializable]
        public struct ItemQuery
        {
            public ItemData itemData;
            public int placementAmount;

            public ItemQuery(ItemData queryItem,int queryAmount)
            {
                itemData = queryItem;
                placementAmount = queryAmount;
            }
        }

        //monobehaviours
        private void Awake()
        {
            //Initialize our references and utilities
            _rectTransform = GetComponent<RectTransform>();
            _layoutGroup = GetComponent<GridLayoutGroup>();
            _layoutGroup.cellSize = _cellSize;
            _unusedStackTextsContainer.gameObject.SetActive(false);

            //Resize the UiWindow.
            ResizeContainer();

        }

        private void Start()
        {
            InitializeGrid();


        }

        private void Update()
        {
            if (_isDebugActive)
                ListenForDebugCommands();
        }
        private void OnDestroy()
        {
            OnGridDestroyed?.Invoke();
        }




        //internals
        private void ResizeContainer()
        {
            Vector2 dynamicSize = new();
            dynamicSize.x = _containerSize.x * _cellSize.x + _layoutGroup.padding.right + _layoutGroup.padding.left;
            dynamicSize.y = _containerSize.y * _cellSize.y + _layoutGroup.padding.bottom + _layoutGroup.padding.top;
            _rectTransform.sizeDelta = dynamicSize;
            _spritesContainer.sizeDelta = dynamicSize;
            _activeStackTextsContainer.sizeDelta = dynamicSize;
            _overlayContainer.sizeDelta = dynamicSize;

            foreach(RectTransform container in _otherContainers)
                container.sizeDelta = dynamicSize;
        }
        private void InitializeGrid()
        {
            //be mindful of the creation order of the cells. GridLayout configured to create them row by row.
            //(0,0) starts at the bottom, similar to the traditional cortesian coord system
            for (int y = 0; y < _containerSize.y; y++)//columns get created after rows
            {
                for (int x = 0; x < _containerSize.x; x++)//rows get created first 
                    CreateNewCell((x, y));
            }
        }
        private GameObject CreateNewCell((int,int) index)
        {
            if (_cellInteractCollection.ContainsKey(index))
            {
                Debug.LogWarning($"Cell ({index.Item1},{index.Item2}) already exists within grid '{gameObject.name}'");
                return null;
            }

            GameObject newCell = Instantiate(_cellPrefab, _rectTransform);
            newCell.gameObject.name = $"Cell ({index.Item1},{index.Item2})";
            CellInteract cellInteract = newCell.GetComponent<CellInteract>();
            cellInteract.SetGrid(this);
            cellInteract.SetIndex(index);
            _cellInteractCollection.Add((index), cellInteract);
            return newCell;

        }



        /// <summary>
        /// Returns all positions that correspond to single stack of items.
        /// The returned indexes together form a key that links to either an itemCode 
        /// or an integer (the number of items in the stack). If nothing is returned
        /// then the position holds no stack of items. Never returns null.
        /// </summary>
        /// <param name="position">The grid position to check.
        /// Any Grid position may belong to only one item stack at a time.</param>
        /// <returns>A new set containing every position of the detected stack.</returns>
        private HashSet<(int, int)> StackArea((int, int) position)
        {
            //look at all the saved stack positionSets
            foreach (HashSet<(int, int)> indexSet in _stackItemDatas.Keys)
            {
                if (indexSet.Contains(position))
                {
                    HashSet<(int,int)> freshIndexSet = new HashSet<(int,int)> ();
                    foreach ((int,int) index in indexSet)
                        freshIndexSet.Add(index);
                    return freshIndexSet;
                }
            }

            //return an empty dataCollection if the position doesn't exist among our saved stacks
            return new();

        }
        private void PositionItemGraphicOntoGridVisually((int, int) index, InvItem item)
        {
            //reparent the item onto the grid visually
            //Get the position of the hovered cell, local to the grid
            Vector3 parentCellPosition = GetCellObject(index).GetComponent<RectTransform>().localPosition;

            RectTransform itemRectTransform = item.GetComponent<RectTransform>();

            //parent the item to the grid's sprite container
            itemRectTransform.SetParent(_spritesContainer, false);
            itemRectTransform.localPosition = parentCellPosition;

            //ensure the sprite is of the appropriate size
            itemRectTransform.sizeDelta = new Vector2(item.Width() * _cellSize.x, item.Height() * _cellSize.y);

            itemRectTransform.gameObject.SetActive(true);

            //ensure the stackText is positioned appropriately
        }
        private void ToggleStackTextViaCurrentAmount(RectTransform uiText, int stackSize)
        {
            if (stackSize <= 1)
                uiText.gameObject.SetActive(false);

            else uiText.gameObject.SetActive(true);
        }
        /*
        public Dictionary<(int, int), InventoryItem> GetItemsInArea(int width, int height, (int, int) clickedGridPosition, (int, int) itemHandle)
        {
            //calculate the item's expected (0,0) position on the grid
            int startingX = clickedGridPosition.Item1 - itemHandle.Item1;
            int startingY = clickedGridPosition.Item2 - itemHandle.Item2;

            Dictionary<(int, int), InventoryItem> foundOccupancy = new Dictionary<(int, int), InventoryItem>();
            (int, int) indexPair;

            //check each cell
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    indexPair = (startingX + i, startingY + j);

                    if (IsCellOnGrid(indexPair))
                    {
                        if (IsCellOccupied(indexPair))
                            foundOccupancy.Add(indexPair, QueryItem(indexPair.Item1, indexPair.Item2));

                    }
                }
            }

            return foundOccupancy;

        }

        public (InventoryItem, Vector2Int newItemHandle) SwapItems(int width, int height, (int, int) clickedGridPosition, (int, int) itemHandle, InventoryItem newItem)
        {
            if (newItem == null)
                return (null, -Vector2Int.one);
            InventoryItem returnedItem = null;

            //take the preexisting item from the grid
            Dictionary<(int, int), InventoryItem> itemsFound = GetItemsInArea(width, height, clickedGridPosition, itemHandle);

            if (itemsFound == null)
            {
                PlaceItem(newItem, clickedGridPosition, itemHandle);
                return (null, -Vector2Int.one);
            }
            else
            {
                //check how many different items occupy the space
                List<InventoryItem> uniqueItems = new List<InventoryItem>();

                foreach (KeyValuePair<(int, int), InventoryItem> entry in itemsFound)
                {
                    if (!uniqueItems.Contains(entry.Value))
                        uniqueItems.Add(entry.Value);
                }

                //perform the swap
                if (uniqueItems.Count == 1)
                {
                    //setup the take operation
                    (int, int) arbitraryIndex = (-1, -1);
                    foreach ((int, int) key in itemsFound.Keys)
                    {
                        arbitraryIndex = key;
                        break;
                    }

                    Vector2Int newHandle = Vector2Int.zero;
                    returnedItem = TakeItem(arbitraryIndex.Item1, arbitraryIndex.Item2, out newHandle);

                    //place the new item at the clickedPosition
                    PlaceItem(newItem, clickedGridPosition, itemHandle);

                    //return the taken item
                    return (returnedItem, newHandle);
                }

                else
                {
                    Debug.LogWarning("Attempted to swap items, but more than one item found in grid area. aborting operation and returning null");
                    return (null, -Vector2Int.one);
                }
            }
        }

        public bool IsAreaUnoccupied(int width, int height, (int, int) clickedGridPosition, (int, int) itemHandle)
        {
            if (GetItemsInArea(width, height, clickedGridPosition, itemHandle).Count == 0)
                return true;
            else return false;
        }

        public void PlaceItem(InventoryItem item, (int, int) clickedPosition, (int, int) itemHandle)
        {
            if (item == null)
                return;

            int itemWidth = item.Width();
            int itemHeight = item.Height();

            //Debug.Log($"Clicked Position: {clickedPosition}");
            Debug.Log($"Item Handle: {itemHandle}"); 
            if (IsAreaUnoccupied(itemWidth, itemHeight, clickedPosition, itemHandle))
            {

                //calculate the item's expected (0,0) position on the grid
                int startingX = clickedPosition.Item1 - itemHandle.Item1;
                int startingY = clickedPosition.Item2 - itemHandle.Item2;

                //save where the item's bottomLeft-most tile exists on the grid
                item.SetRelativeOrigin(startingX, startingY);

                _containedItems.Add(item, new());

                //populate each cell
                for (int i = 0; i < itemWidth; i++)
                {
                    for (int j = 0; j < itemHeight; j++)
                    {
                        int gridPosX = startingX + i;
                        int gridPosY = startingY + j;

                        _containedItems[item].Add((gridPosX, gridPosY));

                        //Debug.Log($"Setting {startingX + i},{startingY + j} to {item.ItemData().Name()}");
                        _items[gridPosX,gridPosY] = item;
                    }
                }

                //parent image to the sprites Container
                RectTransform itemRectTransform = item.GetComponent<RectTransform>();
                itemRectTransform.SetParent(_spritesContainer);

                //offset the image to its origin
                itemRectTransform.localPosition = _cellObjects[startingX,startingY].GetComponent<RectTransform>().localPosition; //sprite currently centered on position

                Vector3 toBottomLeftTileCornerOffset = new();
                toBottomLeftTileCornerOffset.x = itemWidth * CellSize().x/ 2 - CellSize().x / 2;
                toBottomLeftTileCornerOffset.y = itemHeight * CellSize().y/ 2 - CellSize().y / 2;

                itemRectTransform.localPosition += toBottomLeftTileCornerOffset ;

                itemRectTransform.localScale = Vector2.one;
            }
        }

        public InventoryItem TakeItem(int x, int y, out Vector2Int itemHandle)
        {
            InventoryItem querydItem = _items[x, y];

            //calculate the item's handle (local to itself)
            Vector2Int clickedPosition = new Vector2Int(x, y);

            itemHandle = clickedPosition - _items[x, y].GetOriginLocation();
            Debug.Log($"Item Handle: {itemHandle}");
            List<(int, int)> validIndexes = new List<(int, int)>();

            //free up all the cells this item is occupying
            for (int i = 0; i < querydItem.Width(); i++)
            {
                for (int j = 0; j < querydItem.Height(); j++)
                {
                    int xPos = querydItem.GetOriginLocation().x + i;
                    int yPos = querydItem.GetOriginLocation().y + j;
                    //Debug.Log($"Checking if Position {xPos},{yPos} is expected item");

                    InventoryItem foundItem = QueryItem(xPos, yPos);

                    //make sure the item at this position matches 
                    if (foundItem == querydItem)
                    {
                        //save the index to be removed after all spaces have been checked
                        validIndexes.Add((xPos, yPos));

                    }
                    else
                    {
                        Debug.LogError($"" +
                            $"Detected Item mismatch while taking item. " +
                            $"Expected item {querydItem.ItemData().Name()} on cell ({xPos},{yPos})," +
                            $" but found item {foundItem.ItemData().Name()} instead. Aborting take operation");
                        return null;
                    }
                }
            }


            foreach ((int, int) index in validIndexes)
            {
                _items[index.Item1, index.Item2] = null;
                //Debug.Log($"Position {index.Item1},{index.Item2} Freed up");
            }

            _containedItems.Remove(querydItem);

            return querydItem;

        }
        */

        private void IncreaseStack((int, int) position, int increment)
        {
            if (increment <= 0)
                return;

            HashSet<(int, int)> stackArea = StackArea(position);
            if (stackArea.Count <= 0)
                return;

            //get the maximum stack value
            int maxCapacity = _stackItemDatas[stackArea].StackLimit();

            //make sure we don't overshoot the stack's limit
            _stackCapacities[stackArea] = Mathf.Min(_stackCapacities[stackArea] + increment, maxCapacity);

            //update the stack's text
            _stackTexts[stackArea].text = $"{_stackCapacities[stackArea]}";

            //hide or toggle the stack text based on the new amount
            ToggleStackTextViaCurrentAmount(_stackTexts[stackArea].GetComponent<RectTransform>(), _stackCapacities[stackArea]);
        }
        private void DecreaseStack((int, int) position, int decrement)
        {
            HashSet<(int, int)> stackArea = StackArea(position);
            if (stackArea.Count <= 0)
                return;

            _stackCapacities[stackArea] -= decrement;
            _stackTexts[stackArea].text = $"{_stackCapacities[stackArea]}";

            int newCapacity = _stackCapacities[stackArea];

            //show or hide the text depending on the stacksize
            ToggleStackTextViaCurrentAmount(_stackTexts[stackArea].GetComponent<RectTransform>(), newCapacity);

            //delete the stack if we've expended all the items
            if (newCapacity <= 0)
                DeleteStack(position);

        }
        private void DeleteStack((int, int) position)
        {
            HashSet<(int, int)> stackArea = StackArea(position);
            if (stackArea.Count <= 0)
                return;

            //ensure the cells clear their stored item
            foreach (KeyValuePair<(int, int), CellInteract> entry in _cellInteractCollection)
            {
                if (stackArea.Contains(entry.Key))
                    _cellInteractCollection[entry.Key].SetInvItem(null);
            }


            _stackCapacities.Remove(stackArea);
            _stackItemDatas.Remove(stackArea);
            InvItem itemGraphic = _stackSpriteObjects[stackArea];
            _stackSpriteObjects.Remove(stackArea);
            TextMeshProUGUI uiText = _stackTexts[stackArea];
            _stackTexts.Remove(stackArea);

            uiText.GetComponent<RectTransform>().SetParent(_unusedStackTextsContainer, false);
            ItemCreatorHelper.ReturnItemToCreator(itemGraphic);


        }
        private void CreateStack((int, int) position, InvItem item, int amount) //only items have the necessary rotation data to fit within a grid
        {
            if (item == null)
            {
                Debug.LogWarning($"Attempted to create a new item stack using a Null item. Ignoring request.");
                return;
            }

            if (DoesItemGraphicAlreadyExistOnGrid(item))
            {
                Debug.LogWarning($"Attempted to create a new item stack using an item graphic thats currently in use by another item stack ({item.name}). Ignoring request.");
                return;
            }

            if (!IsCellOnGrid(position))
            {
                Debug.LogWarning($"Attempted to create a new item stack ({item.name}) " +
                    $"onto an invalid grid position '({position.Item1},{position.Item2})'. Ignoring request.");
                return;
            }

            //calculate the item's expectedGridPosition
            HashSet<(int, int)> expectedGridOccupancy = ConvertSpacialDefIntoGridArea(position, item.GetSpacialDefinition(), item.ItemHandle());

            //check if all of the positions are within the grid
            if (!IsAreaWithinGrid(expectedGridOccupancy))
            {
                Debug.LogWarning($"Attempted to create a new item stack ({item.name}) to position ({position.Item1},{position.Item2}), " +
                    $"but item won't fit based on the item's spacial definition + itemHandle Combination. Ignoring request.\n" +
                    $"Requested Grid Occupancy:{StringifyPositions(expectedGridOccupancy)}");
                return;
            }


            string overlappedPositions = "";
            bool isAreaOccupied = false;
            //check if all expected positions are available
            foreach ((int, int) index in expectedGridOccupancy)
            {
                if (IsCellOccupied(index))
                {
                    isAreaOccupied = true;
                    overlappedPositions += $"({index.Item1},{index.Item2}): occupied by '{GetStackItemData(index).name}'\n";

                }

                if (isAreaOccupied)
                {
                    Debug.LogWarning($"Attempted to create a new item stack ({item.name}) onto position ({position.Item1},{position.Item2}), " +
                    $"but the item stack's placement overlaps other stacks. Ignoring request.\nDetected overlaps:\n{overlappedPositions}" +
                    $"\nRequested Positions:{StringifyPositions(expectedGridOccupancy)}");
                    return;
                }
            }

            int stackAmount = Mathf.Clamp(amount, 1, item.ItemData().StackLimit());

            //create a textUi to represent the item's stacksize
            RectTransform uiTextTransform = null;

            //either create a new text object, or reuse a discarded one as the new text object 
            if (_unusedStackTextsContainer.childCount == 0)
                uiTextTransform = Instantiate(_stackTextUiPrefab, _activeStackTextsContainer);
            else
            {
                uiTextTransform = _unusedStackTextsContainer.GetChild(0).GetComponent<RectTransform>();
                uiTextTransform.SetParent(_activeStackTextsContainer, false);
            }

            //set the text to match the stack's new value
            TextMeshProUGUI uiText = uiTextTransform.GetComponent<TextMeshProUGUI>();
            uiText.text = $"{stackAmount}";

            //position the text to the lowest, rightmost cell positions
            PositionUiTextOntoStack(uiTextTransform, expectedGridOccupancy);

            //show or hide the text depending on the stacksize
            ToggleStackTextViaCurrentAmount(uiTextTransform, stackAmount);


            //everything is good! create the stack (clamp the amount to legit values)
            _stackItemDatas.Add(expectedGridOccupancy, item.ItemData());
            _stackCapacities.Add(expectedGridOccupancy, stackAmount);
            _stackSpriteObjects.Add(expectedGridOccupancy, item);
            _stackTexts.Add(expectedGridOccupancy, uiText);

            //ensure the cells know what they're storing
            foreach (KeyValuePair<(int,int),CellInteract> entry in _cellInteractCollection)
            {
                if (expectedGridOccupancy.Contains(entry.Key))
                    _cellInteractCollection[entry.Key].SetInvItem(item);
            }

            PositionItemGraphicOntoGridVisually(position, item);

        }

        private void RaiseInvContentsChangeEvent(InvContentsUpdate update)
        {
            OnContentsChanged?.Invoke(update);
        }
        private void RaiseInvContentsChangeEvent(ItemData itemData, int amount, InvOperation operation, List<HashSet<(int,int)>> stacksAffected)
        {
            OnContentsChanged?.Invoke(new InvContentsUpdate(itemData,amount,operation, stacksAffected));
        }
        private void RaiseBulkInvContentsChangeEvent(List<InvContentsUpdate> updateList)
        {
            OnBulkContentsChanged?.Invoke(updateList);
        }

        private IEnumerator RepositionTextAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();

            foreach (KeyValuePair<HashSet<(int, int)>, TextMeshProUGUI> entry in _stackTexts)
                PositionUiTextOntoStack(entry.Value.GetComponent<RectTransform>(), entry.Key);

            _textUpdater = null;
        }
        private void PositionUiTextOntoStack(RectTransform uiText, HashSet<(int, int)> stackPositions)
        {

            //the stack value needs to be on the rightmost, lowest cell value.
            //Find that cell index


            //calculate the item's the rightmost, lowest cell
            int xMaxPosition = 0;
            int yMinPosition = 0;

            //first find the lowest cell that exists
            bool firstIteration = true;

            foreach ((int, int) index in stackPositions)
            {
                if (firstIteration)
                {
                    yMinPosition = index.Item2;
                    firstIteration = false;
                }
                else
                {
                    if (index.Item2 < yMinPosition)
                        yMinPosition = index.Item2;
                }
            }

            //next find the rightmost cell that's also the lowest
            firstIteration = false;

            foreach ((int, int) index in stackPositions)
            {
                if (index.Item2 == yMinPosition)
                {
                    if (firstIteration)
                    {
                        xMaxPosition = index.Item1;
                        firstIteration = false;
                    }
                    else if (index.Item1 > xMaxPosition)
                        xMaxPosition = index.Item1;
                }

            }

            //get the found cell's position on the grid
            Vector2 bottomRightCellPosition = GetCellObject((xMaxPosition, yMinPosition)).GetComponent<RectTransform>().localPosition;

            //set the stackText's transform to that cell's position. Different parents, but both object should be the same size and in the same place
            uiText.localPosition = bottomRightCellPosition;

        }
        private void PositionHoverGraphicOntoGrid(RectTransform graphicRectTransform, (int,int) cellPosition)
        {
            //reparent the graphic onto the grid visually
            //Get the position of the hovered cell, local to the grid
            Vector3 parentCellPosition = GetCellObject(cellPosition).GetComponent<RectTransform>().localPosition;

            //parent the graphic to the grid's overlay container
            graphicRectTransform.SetParent(_overlayContainer, false);
            graphicRectTransform.localPosition = parentCellPosition;

            //ensure the sprite is of the appropriate size
            graphicRectTransform.sizeDelta = new Vector2(_cellSize.x, _cellSize.y);

        }
        /*
        private void UpdateHoverGraphics()
        {
            //ignore invalid cell positions
            if (!IsCellOnGrid(_hoveredCell))
            {
                Debug.LogWarning($"Failed to update hoverGraphics on position ({_hoveredCell.Item1},{_hoveredCell.Item2}): position doesn't exist on grid.");
                return;
            }

            //if no primary hover graphic exists, create one
            if (_primaryHoverGraphic == null)
                _primaryHoverGraphic = Instantiate(_primaryHoverGraphicPrefab);

            //reposition the primary hover graphic onto the focusedPosition
            PositionHoverGraphicOntoGrid(_primaryHoverGraphic.GetComponent<RectTransform>(), _hoveredCell);

            //clear the temp variable
            _hoverGraphics.Clear();

            //if no item is pinned, then infer the hover effects from what's at the grid position
            if (_pinnedRectTransform == null)
            {
                //if the cell isn't occupied, then clear all secondary hover effects
                if (!IsCellOccupied(_hoveredCell))
                    ClearSecondaryHoverGraphics();

                //ensure every cell of the detected item is hovered
                else
                {
                    //clear the temp variables
                    _positions.Clear();
                    _markedHoverGraphics.Clear();

                    //track all positions that the detected item occupies
                    _positions = GetStackArea(_hoveredCell);

                    //mark all preexisting graphics that aren't within the item's occupancy
                    foreach (KeyValuePair<(int, int), GameObject> entry in _showingHoverGraphics)
                    {
                        if (!_positions.Contains(entry.Key))
                            _markedHoverGraphics.Add(entry.Key);
                    }

                    //remove all marked graphics
                    foreach ((int, int) position in _markedHoverGraphics)
                    {
                        _hoverGraphics.Add(_showingHoverGraphics[position]);
                        _showingHoverGraphics.Remove(position);
                    }

                    //destroy the removed graphics (later we'll pool them)
                    while (_hoverGraphics.Count > 0)
                    {
                        GameObject graphic = _hoverGraphics[_hoverGraphics.Count - 1];
                        _hoverGraphics.Remove(graphic);
                        Destroy(graphic);
                    }

                    //create new graphics for every position the item occupies [that doesn't yet have an associated graphic]
                    foreach ((int, int) position in _positions)
                    {
                        if (!_showingHoverGraphics.ContainsKey(position))
                        {
                            GameObject newGraphic = Instantiate(_secondaryHoverGraphicPrefab);
                            _showingHoverGraphics.Add(position, newGraphic);

                            //place the new graphic at it's respective cell position in the overlay layer
                            PositionHoverGraphicOntoGrid(newGraphic.GetComponent<RectTransform>(), position);
                        }
                    }
                }
            }

            //otherwise, always show the potential placement area for the pinned item [unless the area is invalid]
            else
            {
                _positions.Clear();
                InvItem pinnedItem = _pinnedRectTransform.GetComponent<InvItem>();
                _positions = ConvertSpacialDefIntoGridArea(_hoveredCell, pinnedItem.GetSpacialDefinition(), pinnedItem.ItemHandle());

                //clear unnecessary graphics and add missing graphics
                if (IsAreaWithinGrid(_positions))
                {
                    _markedHoverGraphics.Clear();

                    //mark all preexisting graphics that aren't within the item's occupancy
                    foreach (KeyValuePair<(int, int), GameObject> entry in _showingHoverGraphics)
                    {
                        if (!_positions.Contains(entry.Key))
                            _markedHoverGraphics.Add(entry.Key);
                    }

                    //remove all marked graphics
                    foreach ((int, int) position in _markedHoverGraphics)
                    {
                        _hoverGraphics.Add(_showingHoverGraphics[position]);
                        _showingHoverGraphics.Remove(position);
                    }

                    //destroy the removed graphics (later we'll pool them)
                    while (_hoverGraphics.Count > 0)
                    {
                        GameObject graphic = _hoverGraphics[_hoverGraphics.Count - 1];
                        _hoverGraphics.Remove(graphic);
                        Destroy(graphic);
                    }

                    //create new graphics for every position the item occupies [that doesn't yet have an associated graphic]
                    foreach ((int, int) position in _positions)
                    {
                        if (!_showingHoverGraphics.ContainsKey(position))
                        {
                            GameObject newGraphic = Instantiate(_secondaryHoverGraphicPrefab);
                            _showingHoverGraphics.Add(position, newGraphic);

                            //place the new graphic at it's respective cell position in the overlay layer
                            PositionHoverGraphicOntoGrid(newGraphic.GetComponent<RectTransform>(), position);
                        }
                    }
                }
                else ClearSecondaryHoverGraphics();

            }

            
        }
        
        private void ClearSecondaryHoverGraphics()
        {
            //Clear all secondary hover graphics
            foreach (KeyValuePair<(int, int), GameObject> entry in _showingHoverGraphics)
            {
                _hoverGraphics.Add(entry.Value);
            }

            _showingHoverGraphics.Clear();

            //destroy all unused graphics (later we'll pool them)
            while (_hoverGraphics.Count > 0)
            {
                GameObject graphic = _hoverGraphics[_hoverGraphics.Count - 1];
                _hoverGraphics.Remove(graphic);
                Destroy(graphic);
            }

            
        }
        private void ClearPrimaryHoverGraphic()
        {
            //clear the primary hover graphic
            Destroy(_primaryHoverGraphic);
            _primaryHoverGraphic = null;
        }
        private void SetRectTransformToCellPosition(RectTransform itemRectTransform, (int,int) cellPosition)
        {
            //Get the position of the hovered cell, local to the grid
            Vector3 parentCellPosition = GetCellObject(_hoveredCell).GetComponent<RectTransform>().localPosition;
            itemRectTransform.localPosition = parentCellPosition;
        }



        
        /// <summary>
        /// Targets a specific cell on the grid. Displays a hover effect on grid cell (or over the item occupying the cell).
        /// If an item is pinned, then the pinned item will hover over the focused cell's position.
        /// </summary>
        /// <param name="cellPosition">The position to focus on</param>
        public void SetHoveredCell((int,int) cellPosition)
        {
            if (!IsCellOnGrid(cellPosition))
            {
                Debug.LogWarning($"Failed to focus on position ({cellPosition.Item1},{cellPosition.Item2}): position doesn't exist on grid.");
                return;
            }

            (int, int) previousHover = _hoveredCell;
            _hoveredCell = cellPosition;
            UpdateHoverGraphics();

            OnCellHovered?.Invoke(this, _hoveredCell);
        }
        public void ClearHoveredCell()
        {
            //only work if we're focusing on a vaild cell
            if (IsCellOnGrid(_hoveredCell))
            {
                (int,int) previousFocus = _hoveredCell;
                _hoveredCell = (-1, -1);

                ClearSecondaryHoverGraphics();
                ClearPrimaryHoverGraphic();

                OnHoveredCellCleared?.Invoke(this);
            }
        }
        */


        

        //externals
        /// <summary>
        /// Communicates multiple inventory changes as a single transaction.
        /// </summary>
        /// <param name="operationsList">A list of operations that were manually performed</param>
        public void ForceRaiseBulkInvContentsChanged(List<(ItemData, int, InvOperation, List<HashSet<(int,int)>>)> operationsList)
        {
            List<InvContentsUpdate> updatesList = new();
            foreach ((ItemData, int, InvOperation, List<HashSet<(int,int)>>) entry in operationsList)
                updatesList.Add(new InvContentsUpdate(entry.Item1, entry.Item2, entry.Item3, entry.Item4));

            RaiseBulkInvContentsChangeEvent(updatesList);

        }
        /// <summary>
        /// Communicates multiple inventory changes as a single transaction.
        /// </summary>
        /// <param name="operationsList">A list of operations that were manually performed</param>
        public void ForceRaiseBulkInvContentsChanged(List<InvContentsUpdate> updateList) { RaiseBulkInvContentsChangeEvent(updateList); }



        
        public Vector2 CellSize() { return new Vector2(_cellSize.x,_cellSize.y); }
        public Vector2Int ContainerSize() { return new Vector2Int(_containerSize.x,_containerSize.y); }
        /// <summary>
        /// Returns true if the given index lies within bounds of the grid. false otherwise.
        /// </summary>
        /// <param name="cell"></param>
        /// <returns>true if the given index lies within bounds of the grid. false otherwise.</returns>
        public bool IsCellOnGrid((int, int) cell)
        {
            if (cell.Item1 < 0 || cell.Item1 >= _containerSize.x || cell.Item2 < 0 || cell.Item2 >= _containerSize.y)
                return false;
            return true;
        }
        /// <summary>
        /// Returns true if a stack exists on the given position. False otherwise. 
        /// Does not check if the position exists on the grid, and will return false in that case.
        /// </summary>
        /// <param name="position"></param>
        /// <returns>true if a stack exists on the given position. False otherwise.</returns>
        public bool IsCellOccupied((int, int) position)
        {
            //Debug.Log($"Checking 'IsCellOccupied Integrity. Provided Position: ({position.Item1},{position.Item2})\nFound Stack Area at position: " + GetStackArea(position).ToCommaSeparatedString());
            //Debug.Log($"Is Cell Occupied: {GetStackArea(position).Count > 0}");
            if (StackArea(position).Count > 0)
                return true;
            else return false;
        }
        /// <summary>
        /// Returns true if a stack exists on the given position. False otherwise. 
        /// Does not check if the position exists on the grid, and will return false in that case.
        /// </summary>
        /// <param name="position"></param>
        /// <returns>true if a stack exists on the given position. False otherwise.</returns>
        public bool IsCellOccupied(int x, int y)
        {
            return IsCellOccupied((x, y));
        }
        /// <summary>
        /// Checks if the given InvItem reference already exists on the grid.
        /// If this returns true, then there's an issue with how you're adding item sprites to the grid.
        /// Each stack should own its own unique sprite, which can be created by the public (static) ItemCreatorHelper.
        /// </summary>
        /// <param name="itemGraphic">The reference that needs to be checked</param>
        /// <returns>true if the given itemGraphic already exists. false otherwise, or false if the given itemGraphic is null.</returns>
        public bool DoesItemGraphicAlreadyExistOnGrid(InvItem itemGraphic)
        {
            if (itemGraphic == null)
                return false;

            foreach (InvItem itemReference in _stackSpriteObjects.Values)
            {
                if (itemReference == itemGraphic)
                    return true;
            }

            return false;
        }
        /// <summary>
        /// Returns the cell associated with the given index, or null if no cell exists at the index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>the cell associated with the given index, or null if no cell exists at the index.</returns>
        public CellInteract GetCellObject((int, int) index)
        {
            if (!_cellInteractCollection.ContainsKey(index))
                return null;

            return _cellInteractCollection[index];

        }
        /// <summary>
        /// Returns the ItemData that's correlated to the given cell position. 
        /// ItemDatas are reference-types; use the itemData's itemCode to compare equality.
        /// ItemCodes can also be translated back into itemDatas via the public (static) ItemCreatorHelper
        /// </summary>
        /// <param name="index"></param>
        /// <returns>The itemData that's correlated to the given cell position, or null if none exist</returns>
        public ItemData GetStackItemData((int, int) index)
        {
            if (IsCellOnGrid(index))
                if (_stackItemDatas.ContainsKey(StackArea(index)))
                    return _stackItemDatas[StackArea(index)];

            return null;
        }
        /// <summary>
        /// Returns the ItemData that's correlated to the given cell position. 
        /// ItemDatas are reference-types; use the itemData's itemCode to compare equality.
        /// ItemCodes can also be translated back into itemDatas via the public (static) ItemCreatorHelper
        /// </summary>
        /// <param name="index"></param>
        /// <returns>The itemData that's correlated to the given cell position, or null if none exist</returns>
        public ItemData GetStackItemData(int x, int y)
        {
            return GetStackItemData((x, y));
        }
        /// <summary>
        /// Returns the invItem (sprite) that is correlated to the given cell position.
        /// returns null if no invItem exists on the given cell position.
        /// </summary>
        /// <param name="index">The cell position of the invItem (sprite) you're looking for</param>
        /// <returns>The InvItem reference that is correlated to the given cell, or null if none exist</returns>
        public InvItem GetInvItemOnCell((int, int) index)
        {
            if (IsCellOnGrid(index))
            {
                //Debug.Log($"Detected Stack inferred from position '({index.Item1},{index.Item2})':\n " + StringifyPositions(StackArea(index)));
                if (_stackSpriteObjects.ContainsKey(StackArea(index)))
                    return _stackSpriteObjects[StackArea(index)];
            }

            return null;
        }
        /// <summary>
        /// Returns the invItem (sprite) that is correlated to the given cell position.
        /// returns null if no invItem exists on the given cell position.
        /// </summary>
        /// <param name="index">The cell position of the invItem (sprite) you're looking for</param>
        /// <returns>The InvItem reference that is correlated to the given cell, or null if none exist</returns>
        public InvItem GetInvItemOnCell(int x, int y)
        {
            return GetInvItemOnCell((x, y));
        }
        /// <summary>
        /// Returns the size of the stack that is detected on the provided position.
        /// If no stack exists on the provided positions, zero is returned.
        /// </summary>
        /// <param name="position"></param>
        /// <returns>The size of the stack at the given position, or zero if no stack was found.</returns>
        public int GetStackValue((int, int) position)
        {
            HashSet<(int, int)> stackPosition = StackArea(position);
            if (stackPosition.Count > 0)
                return _stackCapacities[stackPosition];

            return 0;
        }
        /// <summary>
        /// Returns the size of the stack that is detected on the provided position.
        /// If no stack exists on the provided positions, zero is returned.
        /// </summary>
        /// <param name="position"></param>
        /// <returns>The size of the stack at the given position, or zero if no stack was found.</returns>
        public int GetStackValue(int x, int y)
        {
            return GetStackValue((x, y));
        }
        /// <summary>
        /// Returns all of the cells that the stack at the provided position is occupying.
        /// Returns an empty collection if no stack exists at the provided position.
        /// Nonexistent grid positions will also return an empty collection.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public HashSet<(int, int)> GetStackArea((int, int) position)
        {
            return StackArea(position);
        }
        /// <summary>
        /// Returns all of the cells that the stack at the provided position is occupying.
        /// Returns an empty collection if no stack exists at the provided position.
        /// Nonexistent grid positions will also return an empty collection.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public HashSet<(int, int)> GetStackArea(int x, int y)
        {
            return GetStackArea((x, y));
        }

        /// <summary>
        /// Converts a series of indexes into gridPositions based on how the item is manually placed into the grid.
        /// Does not check if the returned grid positions are actually on the grid.
        /// </summary>
        /// <param name="selectedGridPosition">The literal clicked position on the grid</param>
        /// <param name="spacialDefinition">The objects size defined as indexes</param>
        /// <param name="itemHandle">The index within the provided spacial definition that should align with the selected grid position</param>
        /// <returns>A set of positions on this grid offset to the specified grid position</returns>
        public HashSet<(int, int)> ConvertSpacialDefIntoGridArea((int, int) selectedGridPosition, HashSet<(int, int)> spacialDefinition, (int, int) itemHandle)
        {
            if (spacialDefinition == null)
                return null;

            if (spacialDefinition.Count < 1)
                return null;

            HashSet<(int, int)> gridPositions = new();

            //convert the provided spacialDefinition into gridPositions.
            foreach ((int, int) index in spacialDefinition)
            {
                // grid index = selectedGridPosition + (currentIndex - itemHandle)       
                int gridX = selectedGridPosition.Item1 + (index.Item1 - itemHandle.Item1);
                int gridY = selectedGridPosition.Item2 + (index.Item2 - itemHandle.Item2);

                gridPositions.Add((gridX, gridY));

            }

            return gridPositions;
        }

        /// <summary>
        /// Counts how many different item stacks exist in a requested area. Returns 0 if a null/empty
        /// area is detected
        /// </summary>
        /// <param name="gridPositions">the grid positions to check</param>
        /// <returns>The count of unique stacks found within the requested area</returns>
        public int CountUniqueStacksInArea(HashSet<(int, int)> gridPositions)
        {
            if (gridPositions == null)
            {
                //Debug.Log("No gridPositions were given to check for Uniqueness. Returning 0, since none we technically found");
                return 0;
            }


            //Save each found stack definition into a set. For convenient uniqueness checking
            //HashSet<HashSet<(int, int)>> uniqueStacks = new();
            HashSet<string> uniqueStacks = new();
            string positionSets = "";
            foreach ((int, int) index in gridPositions)
            {
                HashSet<(int, int)> stackArea = StackArea(index);
                if (stackArea.Count > 0)
                    uniqueStacks.Add(StringifyPositions(stackArea));


                positionSets += $"{StringifyPositions(stackArea)}\n";

            }

            //Debug.Log($"Unique Stacks Detected: {uniqueStacks.Count}\nBreakdown:{positionSets}");
            return uniqueStacks.Count;

        }

        /// <summary>
        /// Checks if every requested position exists within the grid.
        /// Null and empty collections return false.
        /// </summary>
        /// <param name="gridPositions">The collection of positions to check</param>
        /// <returns>True if every grid position exist on the grid. False otherwise, or false if the given collection is invalid</returns>
        public bool IsAreaWithinGrid(HashSet<(int, int)> gridPositions)
        {
            if (gridPositions == null)
                return false;

            if (gridPositions.Count == 0)
                return false;

            foreach ((int, int) index in gridPositions)
            {
                if (!IsCellOnGrid(index))
                    return false;
            }

            return true;
        }



        /// <summary>
        /// Removes the requested amount of whatever that exists at the specified position. If not enough exists then the command is ignored.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="amount"></param>
        /// <param name="suppressOnChangedEvent">If true, then the "OnContentsChanged" event will not fire in response to this current operation. 
        /// Use this if you need to perform multiple operations as one operation before raising the OnContentsChanged event.
        /// If you're doing this, then call 'ForceRaiseBulkInvContentsChanged' after all your operations are performed. 
        /// You'll need to track your performed changes manually, in this case.</param>
        /// <returns>true if all requested items were removed successfully. false otherwise.</returns>
        public bool RemoveItem((int, int) position, int amount, bool suppressOnChangedEvent = false)
        {
            //ignore if no stack exists at the position
            if (StackArea(position).Count == 0)
                return false;

            //Log invalid command if not enough items exist within the stack at the specified position
            if (_stackCapacities[StackArea(position)] < amount)
            {

                //if we got down here, then we didn't find the full amount of items to fulfill the request. Raise a yellow alert. The User probably didn't check the item count beforehand.
                Debug.LogWarning($"Failed to find {amount} items at position [({position.Item1},{position.Item2})]. Found {_stackCapacities[StackArea(position)]} items.");
                return false;
            }
            ItemData itemChanged = GetStackItemData(position);

            //create the list of areas that were changed [All fresh collections]
            List<HashSet<(int, int)>> areasAffected = new();
            areasAffected.Add(StackArea(position));


            DecreaseStack(position, amount);

            //Debug.Log($"Tracked affected stack Areas count: {areasAffected.Count}");
            //Debug.Log($"ItemData inferred: {itemChanged}");

            if (!suppressOnChangedEvent)
                RaiseInvContentsChangeEvent(new InvContentsUpdate(itemChanged, amount, InvOperation.Remove, areasAffected));

            return true;
        }

        /// <summary>
        /// Removes the requested amount of whatever that exists at the specified position. If not enough exists then the command is ignored.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="amount"></param>
        /// <param name="suppressOnChangedEvent">If true, then the "OnContentsChanged" event will not fire in response to this current operation. 
        /// Use this if you need to perform multiple operations as one operation before raising the OnContentsChanged event.
        /// If you're doing this, then call 'ForceRaiseBulkInvContentsChanged' after all your operations are performed. 
        /// You'll need to track your performed changes manually, in this case.</param>
        /// <returns>true if all requested items were removed successfully. false otherwise.</returns>
        public bool RemoveItem(string itemCode, int amount, bool suppressOnChangedEvent = false)
        {
            ItemData itemToFind = ItemCreatorHelper.GetItemDataFromItemCode(itemCode);

            if (itemToFind == null)
            {
                Debug.LogWarning($"Either the ItemCode {itemCode} isn't recognized by the ItemCreator (due to it missing a refernce to that specific item in its itemList), " +
                    $"or the ItemCreator doesn't exist in the scene. Ensure the ItemCreator exists, is active in the scene, and has a reference to all ItemData references. Ignoring Remove request.");
                return false;
            }
            
            if (CountItem(itemToFind) < amount)
            {
                Debug.LogWarning($"Failed to find {amount} items of itemCode [{itemCode} :: {itemToFind.Name()}]. Only found {CountItem(itemToFind)} of {amount} items.");
                return false;
            }

            int remainder = amount;
            int found = 0;
            Dictionary<HashSet<(int, int)>, int> foundAmounts = new Dictionary<HashSet<(int, int)>, int>(HashSet<(int, int)>.CreateSetComparer());

            //create the list of areas that were changed [All fresh collections]
            List<HashSet<(int, int)>> areasAffected = new();

            //check each itemStack's itemData for a matching itemCode
            foreach (KeyValuePair<HashSet<(int, int)>, ItemData> entry in _stackItemDatas)
            {
                if (entry.Value.ItemCode().ToLower() == itemCode.ToLower())
                {

                    //if our amount total was found, remove them all
                    if (_stackCapacities[entry.Key] >= remainder)
                    {
                        //ensure we aren't accidentally exposing the key itself
                        HashSet<(int, int)> newStackAreaSet = entry.Key.ToHashSet();

                        //track the current stack's position for the OnContentsChanged event
                        if (!areasAffected.Contains(newStackAreaSet))
                            areasAffected.Add(newStackAreaSet);

                        //remove any remainder amount from this stack first
                        RemoveItem(newStackAreaSet.First(), remainder,true);


                        HashSet<(int, int)> pastStackArea;

                        //then remove all the recorded amounts from the previous stacks
                        foreach (KeyValuePair<HashSet<(int,int)>,int> stack in foundAmounts)
                        {
                            //before removal, track every past stack's position info
                            pastStackArea = stack.Key.ToHashSet();

                            if (!areasAffected.Contains(pastStackArea))
                                areasAffected.Add(pastStackArea);

                            //remove all items from each past stack
                            RemoveItem(pastStackArea.First(), stack.Value,true);
                        }

                        //Debug.Log($"Tracked affected stack Areas count: {areasAffected.Count}");
                        //Debug.Log($"ItemData inferred: {itemToFind}");

                        //raise the event
                        if (!suppressOnChangedEvent)
                            RaiseInvContentsChangeEvent(new InvContentsUpdate(ItemCreatorHelper.GetItemDataFromItemCode(itemCode), amount, InvOperation.Remove,areasAffected));

                        return true;
                    }

                    //else, save the current stack, reduce the amount by the found stack's capacity, and continue looking for the remainder
                    else
                    {
                        //first, create a fresh collection to ensure we wont accidentally expose the key's reference to externals
                        HashSet<(int, int)> newStackAreaSet = entry.Key.ToHashSet();


                        found += _stackCapacities[newStackAreaSet];
                        foundAmounts[newStackAreaSet] = _stackCapacities[newStackAreaSet];
                        remainder -= _stackCapacities[newStackAreaSet];
                    }
                }
            }

            //if we got down here, then we didn't find the full amount of items to fulfill the request. Raise a yellow alert. The User probably didn't check the item count beforehand.
            Debug.LogWarning($"Failed to find {amount} items of itemCode [{itemCode}]. Only found {found} of {amount} items. [Reached the very end of the method, which shouldnt be possible...]");
            return false;
        }
        /// <summary>
        /// Removes the requested amount of whatever that exists at the specified position. If not enough exists then the command is ignored.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="amount"></param>
        /// <param name="suppressOnChangedEvent">If true, then the "OnContentsChanged" event will not fire in response to this current operation. 
        /// Use this if you need to perform multiple operations as one operation before raising the OnContentsChanged event.
        /// If you're doing this, then call 'ForceRaiseBulkInvContentsChanged' after all your operations are performed. 
        /// You'll need to track your performed changes manually, in this case.</param>
        /// <returns>true if all requested items were removed successfully. false otherwise.</returns>
        public bool RemoveItem(ItemData itemData, int amount, bool suppressOnChangedEvent = false)
        {
            if (itemData == null)
            {
                Debug.LogWarning("NULL itemData detected. Ignoring Removal request.");
                return false;
            }
            return RemoveItem(itemData.ItemCode(), amount, suppressOnChangedEvent);
        }

        /// <summary>
        /// Attempts to place a specified amount of a particular item anywhere in the grid. If not enough space exists in the grid, then no items will be added
        ///  and the operation will fail. Pre-existing stacks will be filled before creating new stacks.
        /// </summary>
        /// <param name="itemData"></param>
        /// /// <param name="suppressOnChangedEvent">If true, then the "OnContentsChanged" event will not fire in response to this current operation. 
        /// Use this if you need to perform multiple operations as one operation before raising the OnContentsChanged event.
        /// If you're doing this, then call 'ForceRaiseBulkInvContentsChanged' after all your operations are performed. 
        /// You'll need to track your performed changes manually, in this case.</param>
        /// <returns>true if all items were added successfully. false otherwise.</returns>
        public bool AddItem(ItemData itemData, int amount, bool suppressOnChangedEvent = false)
        {
            //make sure the item is valid
            if (itemData == null)
            {
                Debug.LogWarning("Attempted to add a Null itemData to the grid. Ignoring request.");
                return false;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"Attempted to add 0 or fewer [{itemData.name}](s) to the grid. Processing request as true");
                return true;
            }

            //Before adding anything, check the invGrid's capacity.

            //create the utilities that'll track all checked spaces
            int totalSpacesFound = 0;

            
            Dictionary<HashSet<(int, int)>, int> availableStacks = new Dictionary<HashSet<(int, int)>, int>(HashSet<(int, int)>.CreateSetComparer());

            List<HashSet<(int, int)>> areasAffected = new();


            //first, find preexisting stacks that aren't yet full
            foreach (KeyValuePair<HashSet<(int,int)>,ItemData> stack in _stackItemDatas)
            {
                //look for each stack that BOTH 1) matches our itemCode AND 2) isn't yet full
                if (stack.Value.ItemCode() == itemData.ItemCode() && _stackCapacities[stack.Key] < itemData.StackLimit())
                {
                    int foundSpace = itemData.StackLimit() - _stackCapacities[stack.Key];


                    //just track this stack's remaining capacity
                    availableStacks[stack.Key] = foundSpace;

                    totalSpacesFound += foundSpace;


                    //break if we don't need to keep searching for space
                    if (totalSpacesFound >= amount)
                        break;
                }
            }

            int remainingAmount = amount;
            int placementAmount;

            //if we found enough vacancies among unfinished stacks, then add the requested amount and return
            if (totalSpacesFound >= amount)
            {
                
                foreach (KeyValuePair<HashSet<(int, int)>, int> stack in availableStacks)
                {
                    //ensure we aren't accidentally exposing the key itself
                    HashSet<(int, int)> newStackAreaSet = stack.Key.ToHashSet();

                    //place either the remainingAmount, or the stack's remaining capacity. Whichever is smallest
                    placementAmount = Mathf.Min(remainingAmount, stack.Value);

                    //place either the remaining items, or the stacks remaining capacity
                    IncreaseStack(newStackAreaSet.First(), placementAmount);

                    //update the remainder. 
                    remainingAmount -= placementAmount;


                    //track the stack as changed/affected
                    if (!areasAffected.Contains(newStackAreaSet))
                        areasAffected.Add(newStackAreaSet);

                    //we've Added the requested items into preexisting stacks
                    if (remainingAmount == 0)
                    {
                        if (!suppressOnChangedEvent)
                            RaiseInvContentsChangeEvent(itemData, amount,InvOperation.Add, areasAffected);
                            
                        return true;
                    }

                }

                //Logically, this part of the code shouldnt ever be reached:
                //We've confirmed that we have enough space in the preexisting stacks.
                //Reaching here means we've failed to add enough items DESPITE having enough space.
                //The previous looping mechanism should be revisited. Make sure we're counting our actions properly.
                Debug.LogWarning($"Counting anomoly during command [Add {amount} {itemData.name}]. " +
                    $"Failed to find a home for {remainingAmount} {itemData.name}(s), despite having enough space within preexisting stacks." +
                    $"\nDiscarding the remaining {remainingAmount} items. Please double check the code's counting");
                return false;
            }




            //otherwise, we need to find more space.

            //save the individual positions to build the reservation list
            HashSet<(int, int)> reservedPositions = new();

            //save exactly how each stack should be organized 
            Dictionary<HashSet<(int,int)>,ItemPlacementData> reservedStacks = new();

            int autoBreakCount = _containerSize.x * _containerSize.y;// if the while runs over the amount of cells that exist, cut it off.
            int iteractionCount = 0;

            //keep finding space for more stacks if we haven't found enough spots [with and autoBrake for added security]
            while (totalSpacesFound < amount && iteractionCount < autoBreakCount)
            {
                
                //check for the next available space.
                HashSet<(int, int)> openAreaForNewStack = FindSpaceForStack(itemData,out (int,int) gridPlacementPosition,out ItemRotation necessaryRotation,reservedPositions);

                //if no positions were found, then we've run out of space [but still require more].
                //Deny the request to add the specified items.
                if (openAreaForNewStack.Count == 0)
                {
                    Debug.LogWarning($"Failed to find enough space for {amount} {itemData.name}(s). Ignoring request.");
                    return false;
                }

                //otherwise, save our findings
                //track the stack's exact placement data
                reservedStacks[openAreaForNewStack] = new ItemPlacementData(gridPlacementPosition, necessaryRotation);
                
                //mark the positions as reserved
                foreach ((int,int) position in openAreaForNewStack)
                    reservedPositions.Add(position);

                //update our amount of spaces found by the item's max stack limit
                totalSpacesFound += itemData.StackLimit();

                //track how many iterations are passing
                iteractionCount++;

            }

            if (iteractionCount >= autoBreakCount)
            {
                Debug.LogWarning($"Cancelled the command to add {amount} {itemData.name}(s) due to not finding enough space within a reasonable amount of iterations [{autoBreakCount}]. Ignoring request.");
                return false;
            }


            //NOW! AFTER ALL THAT WORK!
            //
            //WE FILL THE INVENTORY


            //top off all the preexisting stacks
            foreach (KeyValuePair<HashSet<(int,int)>,int> entry in availableStacks)
            {
                IncreaseStack(entry.Key.First(), entry.Value);
                remainingAmount -= entry.Value;

                //track this stack as edited
                areasAffected.Add(entry.Key.ToHashSet());
            }


            //now for each reserved stack, create the item and add it to the inventory
            foreach (KeyValuePair<HashSet<(int,int)>,ItemPlacementData> entry in reservedStacks)
            {
                //create the new Item
                InvItem newItem = ItemCreatorHelper.CreateItem(itemData, _cellSize.x, _cellSize.y).GetComponent<InvItem>();

                //rotate the item to the saved rotation
                switch (entry.Value.necessaryRotation)
                {
                    case ItemRotation.None:
                        break;
                    case ItemRotation.Once:
                        newItem.RotateItem(RotationDirection.Clockwise);
                        break;
                    case ItemRotation.Twice:
                        newItem.RotateItem(RotationDirection.Clockwise);
                        newItem.RotateItem(RotationDirection.Clockwise);
                        break;
                    case ItemRotation.Thrice:
                        newItem.RotateItem(RotationDirection.CounterClockwise);
                        break;
                }

                placementAmount = Mathf.Min(remainingAmount, itemData.StackLimit());

                //create a new stack using the newly-created, rotated item.
                //Stack size should be the smallest of either the stackLimit OR the remaining amount to place
                CreateStack(entry.Value.gridPlacementPosition, newItem, placementAmount);
                remainingAmount -= placementAmount;

                //track this stack as edited
                areasAffected.Add(entry.Key.ToHashSet());
            }

            //We're Done!
            if (!suppressOnChangedEvent)
                RaiseInvContentsChangeEvent(itemData, amount, InvOperation.Add, areasAffected);
            return true;

        }

        /// <summary>
        /// Attempts to place a specified amount of a particular item at a specified position. If not enough space exists within the specified area,
        ///  then the operation will fail and no items will be placed. If pre-existing stacks exist within the placement area, then any
        ///  compatible stacks (starting from the directly-specified position) will get filled up, assuming enough space exists to fulfill the operation. 
        ///  Otherwise, if the space is empty, then a new stack will be created at the specified position. Any attempt to add beyond the item's own stack limit 
        ///  will likely result in failure.
        /// </summary>
        /// <param name="itemData">the initialized item to add</param>
        /// <param name="amount">The amount to add</param>
        /// <param name="rotation">How rotated whould the item be when placed</param>
        /// <param name="position">The targeted grid placement position. If the item spans many cells, this position will be relative to the item's item handle</param>
        /// <param name="suppressOnChangedEvent">If true, then the "OnContentsChanged" event will not fire in response to this current operation. 
        /// Use this if you need to perform multiple operations as one operation before raising the OnContentsChanged event.
        /// If you're doing this, then call 'ForceRaiseBulkInvContentsChanged' after all your operations are performed. 
        /// You'll need to track your performed changes manually, in this case.</param>
        /// <returns>true if all items were added successfully. false otherwise.</returns>
        public bool AddItem(ItemData itemData, int amount, (int,int) position, ItemRotation rotation, bool suppressOnChangedEvent = false)
        {
            //make sure the item is valid
            if (itemData == null)
            {
                Debug.LogWarning("Attempted to add a Null itemData to the grid. Ignoring AddItem request.");
                return false;
            }

            if (amount > itemData.StackLimit())
            {
                Debug.LogWarning($"Attempted to add a {amount} {itemData.name}(s) to the grid, but the item's stack limit is {itemData.StackLimit()}. " +
                    $"Ignoring AddItem request.");
                return false;
            }

            if (!IsCellOnGrid(position))
            {
                Debug.LogWarning($"Attempted to add a {amount} {itemData.name}(s) to a specified Off-the-grid position ({position.Item1},{position.Item2}). " +
                    $"Ignoring AddItem request.");
                return false;
            }

            HashSet<(int, int)> placementArea = ConvertSpacialDefIntoGridArea(position, itemData.RotatedSpacialDef(rotation), itemData.RotatedItemHandle(rotation));

            //create the collection of edited position, for the OnContentsChanged event
            List<HashSet<(int, int)>> affectedPositions = new();


            //Deliberately AVOID checking if the selected position is directly occupied.
            //What if the ItemHandle isn't within the spacial def? (Could be offset/ or a hollow item)
            //ensure the item's area within the grid fits before doing anything else
            /*
            if (!IsAreaWithinGrid(placementArea))
            {
                Debug.LogWarning($"Item [{itemData.name}] won't fit on grid if placed on position ({position.Item1},{position.Item2}) at rotation {rotation}. Ignoring request." +
                    $"\n Item Spacial Def\n{StringifyPositions(placementArea)}");
                return false;
            }*/

            //Case 1: is the placement area empty? -> place items and return
            bool occupancyDetected = false;
            bool outOfBounds = false;
            foreach ((int,int) cell in placementArea)
            {
                if (!IsCellOnGrid(cell))
                {
                    outOfBounds = true;
                    break;
                }
                if (IsCellOccupied(cell))
                {
                    occupancyDetected = true;
                    break;
                }
            }

            //if nothing occupied the space (& the area is within bounds), create a new stack at the requested position
            if (!occupancyDetected && !outOfBounds)
            {

                GameObject newItemObj = ItemCreatorHelper.CreateItem(itemData, _cellSize.x, _cellSize.y);
                InvItem invItem = newItemObj.GetComponent<InvItem>();
                int rotationsPerformed = 0;
                int maxRotationsPossible = 4;
                while (invItem.Rotation() != rotation && rotationsPerformed < maxRotationsPossible)
                {
                    invItem.RotateItem(RotationDirection.Clockwise);
                    rotationsPerformed++;
                }

                //raise an error if something really weird occurred while finding the proper rotation
                if (invItem.Rotation() != rotation)
                {
                    Debug.LogWarning($"Failed to properly rotate the specified item [{itemData.name}] to" +
                        $" match the requested rotation [{rotation}]. Checked [{rotationsPerformed + 1}] rotations. Ignoring Request");
                    return false;
                }

                CreateStack(position, invItem, amount);

                //track the newly-created stack
                affectedPositions.Add(StackArea(position));

                if (!suppressOnChangedEvent)
                    RaiseInvContentsChangeEvent(itemData, amount, InvOperation.Add, affectedPositions);

                return true;
            }

            //Case 2: The space is either occupied or partially OB. Is there a similar stack at the specified position to add to?
            //The specified position takes priority over any other position within the placement area, since it was deliberately requested first

            //Begin caching all of our needed utilities.
            int remainingSpacesToFind = amount;
            int detectedSpace = 0;
            int placementValue = 0;
            HashSet<(int, int)> blockedPositions = new();
            List<HashSet<(int, int)>> openStacks= new();
            List<int> stackCapacities = new();

            //mark the requested position first, if a compatible stack exists here
            if (IsCellOccupied(position))
            {
                //is this position occupied by a compatible (and unfilled) stack?
                if (GetStackItemData(position).ItemCode() == itemData.ItemCode() && GetStackValue(position) < itemData.StackLimit())
                {
                    //calculate the available space here
                    detectedSpace = itemData.StackLimit() - GetStackValue(position);

                    //calculate the amount we should place here. [either fill the stack or put the remaining items here] 
                    placementValue = Mathf.Min(detectedSpace, remainingSpacesToFind);
                    
                    //track what we've found here
                    openStacks.Add(GetStackArea(position));
                    stackCapacities.Add(detectedSpace);
                    remainingSpacesToFind -= placementValue;

                    //we're definitely adding items here, to track this found stack
                    affectedPositions.Add(StackArea(position));
                    
                    //if we've found space for all the items right here, then we can add everything now and end the operation.
                    if (remainingSpacesToFind == 0)
                    {
                        IncreaseStack(position, placementValue);

                        if (!suppressOnChangedEvent)
                            RaiseInvContentsChangeEvent(itemData, amount, InvOperation.Add, affectedPositions);

                        return true;
                    }
                }
            }
            
            //If we made it this far, then we need to find more space. Now we can look ANYWHERE within the placement area!
            foreach ((int,int) cell in placementArea)
            {
                if (!IsCellOnGrid(cell))
                    continue;

                if (IsCellOccupied(cell))
                {
                    //have we already checked this stack?
                    if (openStacks.Contains(GetStackArea(cell)))
                        continue;

                    //otherwise, is this position occupied by a compatible (and unfilled) stack?
                    if (GetStackItemData(cell).ItemCode() == itemData.ItemCode() && GetStackValue(cell) < itemData.StackLimit())
                    {
                        //calculate the available space
                        detectedSpace = itemData.StackLimit() - GetStackValue(cell);

                        //calculate the amount we should place here
                        placementValue = Mathf.Min(detectedSpace, remainingSpacesToFind);

                        //track what we've found here
                        openStacks.Add(GetStackArea(cell));
                        stackCapacities.Add(detectedSpace);
                        remainingSpacesToFind -= placementValue;

                        //track this found, soon-to-be-filled stack
                        affectedPositions.Add(StackArea(position));

                        //if we've found positions for all of our requested items, then add them to the tracked stacks and end the operation
                        if (remainingSpacesToFind == 0)
                        {
                            for (int i = 0; i <= openStacks.Count; i++)
                                IncreaseStack(openStacks[i].First(), stackCapacities[i]);

                            if (!suppressOnChangedEvent)
                                RaiseInvContentsChangeEvent(itemData, amount, InvOperation.Add,affectedPositions);

                            return true;
                        }
                    }

                    else 
                        blockedPositions.Add(cell);
                }
            }

            //if we've reached this position, then we didn't find enough space.

            //build the debugLog, for further clarity
            string debugString = $"Failed to find enough space at position ({position.Item1},{position.Item2}) for {amount} {itemData.name}(s):\n";

            if (remainingSpacesToFind < amount)
                debugString += $"Only found space for {amount - remainingSpacesToFind} {itemData.name}(s).\n";

            if (blockedPositions.Count > 0)
                debugString += $"Placement blocked on the following cells\n{StringifyPositions(blockedPositions)}\n";

            debugString += " Ignoring request.";

            Debug.LogWarning(debugString);
            return false;

        }

        /// <summary>
        /// Searches the grid for an empty position, defined by the passed item's spacial definition. 
        /// DOES NOT ATTEMPT TO AUTOFILL OTHER PREEXISTING COMPATIBLE STACKS. 
        /// </summary>
        /// <param name="itemData"></param>
        /// <param name="gridPosition"></param>
        /// <param name="necessaryRotation"></param>
        /// <param name="excludedPositions"></param>
        /// <returns></returns>
        public HashSet<(int,int)> FindSpaceForStack(ItemData itemData, out (int, int) gridPosition, out ItemRotation necessaryRotation, HashSet<(int,int)> excludedPositions)
        {
            gridPosition = (-1, -1);
            necessaryRotation = ItemRotation.None;

            //this parameter is optional. But make sure its not null
            if (excludedPositions == null)
                excludedPositions = new();

            //Debug.Log($"Excluded Positions received: {StringifyPositions(excludedPositions)}");

            if (itemData == null)
            {
                Debug.LogWarning("Attempted to find space for a stack with a NULL itemData. Returning an empty collection");
                return new();
            }

            
            //setup the iteration utilities
            int width = _containerSize.x;
            int height = _containerSize.y;
            int rotationCount = 0;

            HashSet<(int, int)> calculatedPositions = new();

            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    //Debug.Log($"Starting Iteration ({w},{h})");
                    //skip cells that're either directly occupied or are explicitly excluded
                    if (IsCellOccupied((w, h)) || excludedPositions.Contains((w,h)))
                    {
                        /*Log Skipped Iterations
                        if (IsCellOccupied((w, h)))
                            Debug.Log($"Tracing 'FindSpaceForStack':\n Cell iteration: ({w},{h})\nStatus: SKIPPED [CELL OCCUPIED]");
                        else 
                            Debug.Log($"Tracing 'FindSpaceForStack':\n Cell iteration: ({w},{h})\nStatus: SKIPPED [CELL MARKED AS EXCLUDED]");
                        */
                        continue;
                    }


                    //check if an itemData fits with its origin (itemHandle) centered on this cell
                    //check all rotations
                    while (rotationCount < 4)
                    {
                        
                        switch (rotationCount)
                        {
                            case 0:
                                necessaryRotation = ItemRotation.None;
                                break;
                            case 1:
                                necessaryRotation = ItemRotation.Once;
                                break;
                            case 2:
                                necessaryRotation = ItemRotation.Twice;
                                break;
                            case 3:
                                necessaryRotation = ItemRotation.Thrice;
                                break;
                        }

                        /* Log the rotated spaces being checked
                        Debug.Log($"Checking Rotation: {necessaryRotation.ToString()}\n" +
                            $"Rotated SpacialDef: {StringifyPositions(itemData.RotatedSpacialDef(necessaryRotation))}\n" +
                            $"Rotated ItemHandle: ({itemData.RotatedItemHandle(necessaryRotation).Item1},{itemData.RotatedItemHandle(necessaryRotation).Item2})");
                        */

                        //calculate the items expected ROTATED spacialData [without going through the trouble of actually creating an item]
                        calculatedPositions = ConvertSpacialDefIntoGridArea((w, h), itemData.RotatedSpacialDef(necessaryRotation), itemData.RotatedItemHandle(necessaryRotation));

                        /* Log Area Check results
                        Debug.Log($"Is area within grid: {IsAreaWithinGrid(calculatedPositions)}\n Positions: {StringifyPositions(calculatedPositions)}");
                        */

                        //first, make sure the space is cleared and valid
                        if (CountUniqueStacksInArea(calculatedPositions) == 0 && IsAreaWithinGrid(calculatedPositions))
                        {
                            bool isExcludedSpaceDetected = false;

                            //next, make sure no no excluded spaces are within the calculated positions
                            foreach ((int,int) position in calculatedPositions)
                            {
                                if (excludedPositions.Contains(position))
                                {
                                    isExcludedSpaceDetected = true;
                                    break;
                                }
                            }

                            //if no excluded spaces were found within this set of positions, then return this set of positions as a valid placement area
                            if (!isExcludedSpaceDetected)
                            {
                                gridPosition = (w, h);

                                /* Log the current iteration's success
                                Debug.Log($"Tracing 'FindSpaceForStack':\n Cell iteration: ({w},{h})\nStatus: success! Returning '{calculatedPositions.Count()}' calculatedPositions.\n DesiredRotation: {necessaryRotation}");
                                */

                                return calculatedPositions;
                            }

                            
                        }

                        rotationCount++;
                    }

                    //Log no space found for none of the current iterations rotations
                    //Debug.Log($"Tracing 'FindSpaceForStack':\n Cell iteration: ({w},{h})\nStatus: NO SPACE FOUND");

                    //none found. Reset the rotationCount and move on to the next cell
                    rotationCount = 0;
                }
            }

            //None were found. Log total failure
            //Debug.Log($"Tracing 'FindSpaceForStack':\n No Positions Found. Returning an empty collection...");
            return new();
        }

        /// <summary>
        /// Checks the grid if space exists for an amount of items. checks preexisting stacks first, then empty positions.
        /// </summary>
        /// <param name="itemQuery"></param>
        /// <returns></returns>
        public bool DoesSpaceExist(ItemData itemData, int amount, HashSet<(int,int)> excludedPositions)
        {
            if (itemData == null)
            {
                Debug.LogWarning("Requested if space exists for a null itemData. Returning false.");
                
                return false;
            }

            if (excludedPositions == null)
                excludedPositions = new HashSet<(int,int)>();

            //Debug.Log($"Excluded Positions received: {StringifyPositions(excludedPositions)}");

            //create the utilities that'll track all checked spaces
            int totalSpacesFound = 0;


            Dictionary<HashSet<(int, int)>, int> availableStacks = new Dictionary<HashSet<(int, int)>, int>(HashSet<(int, int)>.CreateSetComparer());

            //first, find preexisting stacks that aren't yet full
            foreach (KeyValuePair<HashSet<(int, int)>, ItemData> stack in _stackItemDatas)
            {
                bool isStackOffLimits = false;
                //ensure the none of the stack's positions are marked as 'excluded' from the space check
                foreach ((int,int) position in stack.Key)
                {
                    if (excludedPositions.Contains(position))
                    {
                        isStackOffLimits = true;
                        break;
                    }
                }

                //skip this current stack if any position was flagged as 'excluded'
                if (isStackOffLimits)
                    continue;

                //look for each stack that BOTH 1) matches our itemCode AND 2) isn't yet full
                if (stack.Value.ItemCode() == itemData.ItemCode() && _stackCapacities[stack.Key] < itemData.StackLimit())
                {
                    int foundSpace = itemData.StackLimit() - _stackCapacities[stack.Key];

                    //just track this stack's remaining capacity
                    availableStacks[stack.Key] = foundSpace;

                    totalSpacesFound += foundSpace;

                    //break if we don't need to keep searching for space
                    if (totalSpacesFound >= amount)
                        break;
                }
            }

            //if we found enough vacancies among unfinished stacks, then return true
            if (totalSpacesFound >= amount)
                return true;



            //otherwise, we need to find more space.
            int remainingAmount = amount;

            //get ready to save the individual positions of potential stacks [and excluded positions] ]to build the reservation list
            HashSet<(int, int)> reservedPositions = new();

            //don't forget to add the excluded positions to our list of reserved positions, here
            foreach ((int,int) position in excludedPositions)
                reservedPositions.Add(position);


            int autoBreakCount = _containerSize.x * _containerSize.y; ;// if the while runs over all the cells, cut it off.
            int iteractionCount = 0;

            //keep finding space for more stacks if we haven't found enough spots [with and autoBrake for added security]
            while (totalSpacesFound < amount && iteractionCount < autoBreakCount)
            {

                //check for the next available space.
                HashSet<(int, int)> openAreaForNewStack = FindSpaceForStack(itemData, out (int, int) gridPlacementPosition, out ItemRotation necessaryRotation, reservedPositions);

                //if no positions were found, then we've run out of space [but still require more].
                //not enough space exists for the stated amount of items
                if (openAreaForNewStack.Count == 0)
                    return false;

                //otherwise, we've found a suitable position for a stack.
                //reserve the found positions.
                foreach ((int, int) position in openAreaForNewStack)
                    reservedPositions.Add(position);

                //update our amount of spaces found by the item's max stack limit
                totalSpacesFound += itemData.StackLimit();

                //track how many iterations are passing
                iteractionCount++;

            }

            if (totalSpacesFound >= amount)
                return true;

            if (iteractionCount >= autoBreakCount)
            {
                Debug.LogWarning($"Cancelled the command to add {amount} {itemData.name}(s) due to not finding enough space within a reasonable amount of iterations [{autoBreakCount}]. Ignoring request.");
                return false;
            }

            Debug.LogWarning("Reached the end of the spaceFind utility. We shouldn't have reached this point in the code. " +
                "This means we somehow didn't find enough, but also failed to detect that we ran out of space.");
            return false;
        }

        /// <summary>
        /// Checks if space exists for an amount of items and returns an ordered list of the necessary placements needed to fulfill the placements. 
        /// Reads and Updates an 'unregistered stack changes' parameter to ensure reiterative space reading-- allowing for a persistent tracking of
        /// used & reserved space, as long as the 'unregistered stack changes' utility is reapplied to future function calls.
        /// </summary>
        /// <param name="itemData"></param>
        /// <param name="amount"></param>
        /// <param name="excludedPositions"></param>
        /// <param name="unregisteredStackChanges">
        /// A collection that keeps what stacks/positions have been previously counted [along with each stack's updated occupancy]. 
        /// This argument will be modified/written to, and acts as a supplement to help remember what has been counted in the past
        /// </param>
        /// <param name="unregisteredStackTypes">
        /// A sister collection to the 'unregistered stack changes' argument. Holds the type of each unregistered stack, in case new ones are created
        /// </param>
        /// <returns></returns>
        private List<ItemQueryResponse> FindSpaceForItems(ItemData itemData,int amount,HashSet<(int, int)> excludedPositions, 
            Dictionary<HashSet<(int,int)>,int> unregisteredStackChanges,Dictionary<HashSet<(int, int)>, ItemData> unregisteredStackTypes)
        {

            if (excludedPositions == null)
                excludedPositions = new HashSet<(int, int)>();

            if (unregisteredStackChanges == null)
                unregisteredStackChanges = new();

            if (unregisteredStackTypes == null)
                unregisteredStackTypes = new();

            if (itemData == null)
            {
                Debug.LogWarning("Requested if space exists for a null itemData. Returning false.");

                return null;
            }

            List<ItemQueryResponse> queryResponse = new();

            //create the utilities that'll track all checked spaces
            int remainingSpace = amount;

            //create a new variable to track our counting updates
            //without directly changing the unregisteredStacks data too early
            Dictionary<HashSet<(int, int)>, int> tempStackUpdates = new Dictionary<HashSet<(int, int)>, int>(HashSet<(int, int)>.CreateSetComparer());
            Dictionary<HashSet<(int, int)>, ItemData> tempStackTypes = new Dictionary<HashSet<(int, int)>, ItemData>(HashSet<(int, int)>.CreateSetComparer());


            //first, find preexisting stacks that aren't yet full
            foreach (KeyValuePair<HashSet<(int, int)>, ItemData> stack in _stackItemDatas)
            {
                bool isStackOffLimits = false;
                //ensure the none of the stack's positions are marked as 'excluded' from the space check
                foreach ((int, int) position in stack.Key)
                {
                    if (excludedPositions.Contains(position))
                    {
                        isStackOffLimits = true;
                        break;
                    }
                }

                //skip this current stack if any position was flagged as 'excluded'
                if (isStackOffLimits)
                    continue;

                //look for each stack that matches our itemCode
                if (stack.Value.ItemCode() == itemData.ItemCode())
                {
                    //check if this stack has any unregistered updates
                    if (unregisteredStackChanges.ContainsKey(stack.Key))
                    {
                        //skip this stack if the UPDATES say it's full
                        if (unregisteredStackChanges[stack.Key] == itemData.StackLimit())
                            continue;

                        //calculate how much space we've found here
                        int foundSpace = itemData.StackLimit() - unregisteredStackChanges[stack.Key];

                        //calculate how much space we'll take from this stack [either everything we've found, or just what we need]
                        int spaceToComsume = Mathf.Min(remainingSpace, foundSpace);

                        //create a new queryResponse for this available position
                        //first create a new copy of our current stack's positions
                        HashSet<(int, int)> savedQueryStack = new();
                        HashSet<(int, int)> savedUpdatedStack = new();
                        foreach ((int, int) position in stack.Key)
                        {
                            savedQueryStack.Add(position);
                            savedUpdatedStack.Add(position);
                        }

                        ItemQueryResponse response = new ItemQueryResponse(itemData, (-1, -1), spaceToComsume, savedQueryStack, ItemRotation.None);

                        //add this individual response to the response list
                        queryResponse.Add(response);

                        //now track what we've counted & filled
                        tempStackUpdates.Add(savedUpdatedStack, unregisteredStackChanges[stack.Key] + spaceToComsume);
                        tempStackTypes.Add(savedUpdatedStack, itemData);

                        //reduce our remaining space by whatever we've found [unless our remaining space needed is less]
                        remainingSpace -= spaceToComsume;

                        //break if we don't need to keep searching for space
                        if (remainingSpace == 0)
                            break;
                    }

                    //otherwise, this stack hasn't been tracked. Check if we have any space available.
                    else if (_stackCapacities[stack.Key] < itemData.StackLimit())
                    {
                        //calculate how much space we've found here
                        int foundSpace = itemData.StackLimit() - _stackCapacities[stack.Key];

                        //calculate how much space we'll take from this stack [either everything we've found, or just what we need]
                        int spaceToComsume = Mathf.Min(remainingSpace, foundSpace);


                        //create a new queryResponse for this available position
                        //first create a new copy of our current stack's positions
                        HashSet<(int, int)> savedStack = new();
                        HashSet<(int, int)> savedUpdatedStack = new();
                        foreach ((int, int) position in stack.Key)
                        {
                            savedStack.Add(position);
                            savedUpdatedStack.Add(position);
                        }

                        //build this stack's query response
                        ItemQueryResponse response = new ItemQueryResponse(itemData, (-1, -1), spaceToComsume, savedStack, ItemRotation.None);

                        //add this individual response to the response list
                        queryResponse.Add(response);

                        //now track the sum of the following: 1) what we've counted before and 2) what we've just taken
                        tempStackUpdates.Add(savedUpdatedStack, _stackCapacities[stack.Key] + spaceToComsume);
                        tempStackTypes.Add(savedUpdatedStack, itemData);

                        //reduce our remaining space by whatever we've found [unless our remaining space needed is less]
                        remainingSpace -= spaceToComsume;

                        //break if we don't need to keep searching for space
                        if (remainingSpace == 0)
                            break;
                    }
                }
            }

            //next, find preexisting, UNREGISTERED stacks that aren't yet full [assuming we haven't met our quota]
            foreach (KeyValuePair<HashSet<(int,int)>,ItemData> stack in unregisteredStackTypes)
            {
                
                //check if we've met our quota yet. Break if we have. No need to continue in this case
                if (remainingSpace == 0)
                {
                    Debug.Log("Breakout reached due to query'd capacity found");
                    break;
                }

                bool isStackOffLimits = false;
                (int,int) detectedExclusion = (-1,-1);
                //ensure the none of the stack's positions are marked as 'excluded' from the space check
                foreach ((int, int) position in stack.Key)
                {
                    if (excludedPositions.Contains(position))
                    {
                        isStackOffLimits = true;
                        detectedExclusion = position;
                        break;
                    }
                }

                //skip this current stack if any position was flagged as 'excluded'
                if (isStackOffLimits)
                {
                    Debug.Log($"Ignoring unregistered stack [{StringifyPositions(stack.Key)}] due to position {detectedExclusion} being marked as excluded.");
                    continue;
                }

                
                //look for each unregistered stack that matches our itemCode [assuming it has any capacity left]
                if (stack.Value.ItemCode() == itemData.ItemCode())
                {
                    if (unregisteredStackChanges[stack.Key] == itemData.StackLimit())
                    {
                        Debug.Log($"Unregistered stack [{StringifyPositions(stack.Key)}] current capacity is maxed [capacity:{unregisteredStackChanges[stack.Key]}]");
                        continue;
                    }

                    Debug.Log($"current capacity of found suitable unregistered stack [{StringifyPositions(stack.Key)}]: {unregisteredStackChanges[stack.Key]}");
                    //calculate how much space we've found here
                    int foundSpace = itemData.StackLimit() - unregisteredStackChanges[stack.Key];

                    //calculate how much space we'll take from this stack [either everything we've found, or just what we need]
                    int spaceToComsume = Mathf.Min(remainingSpace, foundSpace);

                    //create a new queryResponse for this available position
                    //first create a new copy of our current stack's positions
                    HashSet<(int, int)> savedQueryStack = new();
                    HashSet<(int, int)> savedUpdatedStack = new();
                    foreach ((int, int) position in stack.Key)
                    {
                        savedQueryStack.Add(position);
                        savedUpdatedStack.Add(position);
                    }

                    //build this stack's query response
                    ItemQueryResponse response = new ItemQueryResponse(itemData, (-1, -1), spaceToComsume, savedQueryStack, ItemRotation.None);

                    //add this individual response to the response list
                    queryResponse.Add(response);

                    //now track what we've counted & filled
                    tempStackUpdates.Add(savedUpdatedStack, unregisteredStackChanges[stack.Key] + spaceToComsume);
                    tempStackTypes.Add(savedUpdatedStack, itemData);

                    //reduce our remaining space by whatever we've found [unless our remaining space needed is less]
                    remainingSpace -= spaceToComsume;

                    //break if we don't need to keep searching for space
                    if (remainingSpace == 0)
                        break;

                }
            }


            //if we've found space for all query'd items, then return our collected queryResponses
            if (remainingSpace == 0)
            {
                //apply our updated counts to the unregistered stack updates
                foreach (KeyValuePair<HashSet<(int,int)>,int> stack in tempStackUpdates)
                {
                    //update any preexisting stacks
                    if (unregisteredStackChanges.ContainsKey(stack.Key))
                        unregisteredStackChanges[stack.Key] = stack.Value;

                    else 
                        unregisteredStackChanges.Add(stack.Key, stack.Value);
                }

                //no new stacks were created, so there's no need to update the tempStackTypes utility
                //just return the queryResonse list
                return queryResponse;
            }


            //get ready to save the individual positions of potential stacks [and excluded positions] ]to build the reservation list
            HashSet<(int, int)> reservedPositions = new();

            //don't forget to add the excluded positions to our list of reserved positions, here
            foreach ((int, int) position in excludedPositions)
                reservedPositions.Add(position);

            //Debug.Log($"Unregistered stack changes size before the reservations list is built: {unregisteredStackChanges.Count}");

            //also be sure to add every position within our unregistered stack updates to the list of reserved positions
            foreach (KeyValuePair<HashSet<(int,int)>, int> stack in unregisteredStackChanges)
            {
                foreach((int, int) position in stack.Key)
                    reservedPositions.Add(position); //Doing this will allow our algorithm to also ignore any previously claimed positions
            }

            //Debug.Log($"Reserved Positions pre stack allocation: {StringifyPositions(reservedPositions)}");

            int autoBreakCount = _containerSize.x * _containerSize.y; ;// if the while runs over all the cells, cut it off.
            int iteractionCount = 0;

            //keep finding space for more stacks if we haven't found enough spots [with and autoBrake for added security]
            while (remainingSpace > 0 && iteractionCount < autoBreakCount)
            {
                
                //check for the next available space.
                HashSet<(int, int)> openAreaForNewStack = FindSpaceForStack(itemData, out (int, int) foundPlacementPosition, out ItemRotation neededRotation, reservedPositions);

                //if no positions were found, then we've run out of space [but still require more].
                //not enough space exists for the stated amount of items
                if (openAreaForNewStack.Count == 0)
                {
                    Debug.Log("Failed to find space");
                    return null;
                }

                //track either the remaining space we need, or the full stack's occupancy. Whatever's smaller
                int foundSpace = Mathf.Min(remainingSpace, itemData.StackLimit());

                //otherwise, we've found a suitable position for a stack.
                //reserve the found positions. also save them
                HashSet<(int, int)> savedQueryStack = new();
                HashSet<(int, int)> savedUpdatedStack = new();
                foreach ((int, int) position in openAreaForNewStack)
                {
                    reservedPositions.Add(position);
                    savedQueryStack.Add(position);
                    savedUpdatedStack.Add(position);
                }

                remainingSpace -= foundSpace;

                //add the built response to the list of responses
                queryResponse.Add(new ItemQueryResponse(itemData, foundPlacementPosition, foundSpace, savedQueryStack, neededRotation));

                //now track what we've counted & filled
                tempStackUpdates.Add(savedUpdatedStack, foundSpace);
                tempStackTypes.Add(savedUpdatedStack, itemData);

                //track how many iterations are passing
                iteractionCount++;

            }

            if (remainingSpace == 0)
            {
                //apply our updated counts to the unregistered stack updates
                foreach (KeyValuePair<HashSet<(int, int)>, int> stack in tempStackUpdates)
                {
                    //update any preexisting stacks
                    if (unregisteredStackChanges.ContainsKey(stack.Key))
                        unregisteredStackChanges[stack.Key] = stack.Value;

                    else
                        unregisteredStackChanges.Add(stack.Key, stack.Value);
                }

                //also apply our updates to unregistered stackTypes collection
                foreach (KeyValuePair<HashSet<(int,int)>,ItemData> stack in tempStackTypes)
                {
                    //add all of the new stacks what were 'created' and counted
                    if (!unregisteredStackTypes.ContainsKey(stack.Key))
                        unregisteredStackTypes.Add(stack.Key,stack.Value);
                }

                return queryResponse;
            }
                

            if (iteractionCount >= autoBreakCount)
            {
                Debug.LogWarning($"Cancelled the command to find space for {amount} {itemData.name}(s) due to not finding enough space within a reasonable amount of iterations [{autoBreakCount}]. Ignoring request.");
                return null;
            }

            Debug.LogWarning("Reached the end of the spaceFind utility. We shouldn't have reached this point in the code. " +
                "This means we somehow didn't find enough, but also failed to detect that we ran out of space.");
            return null;
        }


        /// <summary>
        /// Iterates through the provided query list and returns whether or not all queries can fit within the grid.
        /// Placement results many differ, depending on the placement order. It's recommended to query (& place) the largest items first.
        /// </summary>
        /// <param name="queryList"></param>
        /// <returns>An ordered list of the necessary placement positions, rotations, placement amounts, 
        /// and full stack areas, along with the expected itemData. 
        /// Placement positions results that reflect (-1,-1) implies the existence of a compatible stack.</returns>
        public List<ItemQueryResponse> FindSpaceForItems(List<ItemQuery> queryList)
        {
            if (queryList == null)
            {
                Debug.LogWarning($"Passed a null queryList while attempting to find space for a list of items. Returning false");
                return null;
            }

            if (queryList.Count == 0)
            {
                Debug.LogWarning($"Passed an empty queryList while attempting to find space for a list of items. Returning false");
                return null;
            }

            Dictionary<HashSet<(int, int)>, int> stackCounts = new Dictionary<HashSet<(int, int)>, int>(HashSet<(int, int)>.CreateSetComparer());
            Dictionary<HashSet<(int, int)>, ItemData> stackTypes = new Dictionary<HashSet<(int, int)>, ItemData>(HashSet<(int, int)>.CreateSetComparer());

            List<ItemQueryResponse> totalQueryResponse = new List<ItemQueryResponse>();
            List<ItemQueryResponse> tempQueryResponse = new();
            //string debugString;
            int iterationCount = 1;

            foreach (ItemQuery query in queryList)
            {
                //Debug.Log($"Tracked stacks size: {stackCounts.Count}");


                tempQueryResponse = FindSpaceForItems(query.itemData, query.placementAmount, null, stackCounts, stackTypes);
                if (tempQueryResponse == default)
                    return null;

                /*
                debugString = $"iteration: {iterationCount}\nStackUpdates [{stackCounts.Count} stacks]:\n";
                foreach (KeyValuePair<HashSet<(int, int)>, int> stack in stackCounts)
                {
                    Debug.Log($"Building debug string. Stack[{StringifyPositions(stack.Key)}]");
                    debugString += $"> Item: {stackTypes[stack.Key].name}(s)\n" +
                        $"> Placement: {StringifyPositions(stack.Key)}\n" +
                        $"> Occupied Capacity: {stack.Value}\n" +
                        $"------------------------\n";
                }
                Debug.Log(debugString);
                */

                foreach (ItemQueryResponse response in tempQueryResponse)
                    totalQueryResponse.Add(response);
                iterationCount++;
            }

            /*
            string responseString = "Query Responses:\n";
            foreach (ItemQueryResponse response in totalQueryResponse)
            {
                responseString += $"----------------------\n" +
                    $"Item: {response.itemData.name}\n" +
                    $"Open Placement: {StringifyPositions(response.reservedPositions)}\n" +
                    $"Amount To Place Here: {response.availableCapacity}\n";
            }
            Debug.Log(responseString);
            */
            return totalQueryResponse;
        }



        /// <summary>
        /// Iterates through the provided query list and returns whether or not all queries can fit within the grid.
        /// Placement results many differ, depending on the placement order. It's recommended to query (& place) the largest items first.
        /// </summary>
        /// <param name="queryList"></param>
        /// <returns>true if all items fit</returns>
        public bool DoesSpaceExist(List<ItemQuery> queryList)
        {
            if (FindSpaceForItems(queryList) != null)
                return true;
            else return false;
        }

        /* This utility is slow. It works, but it's really slow. 
        public int HowManyCanFit(ItemData itemData)
        {
            if (itemData == null)
            {
                Debug.LogWarning("Null item data passed as parameter [on 'HowManyCanFit' request]. returning 0");
                return 0;
            }

            int amount = 0;
            List<ItemQuery> queryList = new List<ItemQuery>();
            ItemQuery newQuery = new ItemQuery(itemData, 1);

            queryList.Add(newQuery);
            amount++;

            //check if at least one item can fit before we try more
            //due this because 'DoesSpaceExist' doesn't answer 'dOeS zErO ItEmS fIt???' questions
            if (!DoesSpaceExist(queryList))
            {
                //not even 1 item will fit. return 0
                return 0;
            }

            //now keep expanding the query list until max capacity is met
            while (DoesSpaceExist(queryList))
            {
                queryList.Add(newQuery);
                amount++;
            }

            return amount;

        }
        */

        public RectTransform GetOverlayRectTransform() { return _overlayContainer; }

        /// <summary>
        /// Increments the width and height of the grid by the passed parameters, appending to the furthest elements.
        /// Then resizes the grid. Negative values are defaulted to zero, meaning that dimesion will be ignored.
        /// </summary>
        /// <param name="xPositions">The (positive) amount of width positions to append.</param>
        /// <param name="yPositions">The (positive) amount of height positions to append.</param>
        /// <param name="suppressResizeEvent">If true, silences the OnResize event. Useful if you're performing many resizes per frame, and dont want
        /// to spam event-responses before your entire resize is complete.</param>
        public void ExpandGrid(int xPositions, int yPositions, bool suppressResizeEvent = false)
        {
            //ensure we're accepting no negative values
            xPositions = Mathf.Max(xPositions, 0);
            yPositions = Mathf.Max(yPositions, 0);

            //return if no expansion is necessary
            if (xPositions == 0 && yPositions == 0)
                return;


            List<(int,int)> newIndexes = new List<(int, int)>();
            int indexPosition = -1;

            //starting from (0,0) -> (1,0) -> (1,0) -> etc, record all the new "beyond container" indexes
            for (int y = 0; y < _containerSize.y + yPositions; y++)
            {
                for (int x = 0; x < _containerSize.x + xPositions; x++)
                {
                    indexPosition++;
                    //ignore all cells that already exist
                    if (IsCellOnGrid((x, y)))
                        continue;

                    //otherwise, create the new Out-of-Bounds cells
                    GameObject newCell = CreateNewCell((x, y));

                    //if creation was successful
                    if (newCell != null)
                    {

                        //place cell in it's appropriate position on the grid.
                        newCell.GetComponent<RectTransform>().SetSiblingIndex(indexPosition);

                    }
                }
            }

            _containerSize.y = _containerSize.y + yPositions;
            _containerSize.x = _containerSize.x + xPositions;

            //resize the window, too.
            ResizeContainer();
            
            if (_textUpdater == null)
            {
                _textUpdater = RepositionTextAtEndOfFrame();
                StartCoroutine(_textUpdater);
            }
            else
            {
                StopCoroutine(_textUpdater);
                _textUpdater = RepositionTextAtEndOfFrame();
                StartCoroutine(_textUpdater);
            }

            if (!suppressResizeEvent)
                OnGridResized?.Invoke(_containerSize);

        }

        /// <summary>
        /// Decrements the width and height of the grid by the passed parameters, starting from the furthest elements.
        /// Then resizes the grid. If items exist within the reduction space, then the operation will fail (This does not remove any items). 
        /// If any parameter would reduces the grid below "1x1", then that parameter is defaulted to 0 (meaning that dimension will be ignored).
        /// </summary>
        /// <param name="xPositions">The (positive) amount of width positions to trim.</param>
        /// <param name="yPositions">The (positive) amount of height positions to trim.</param>
        /// <param name="suppressResizeEvent">If true, silences the OnResize event. Useful if you're performing many resizes per frame, and dont want
        /// to spam event-responses before your entire resize is complete.</param>
        public void ReduceGrid(int xPositions, int yPositions, bool suppressResizeEvent = false)
        {
            //ensure we're accepting no negative values
            xPositions = Mathf.Max(xPositions, 0);
            yPositions = Mathf.Max(yPositions, 0);

            //if any of our parameters would reduce the grid's dimensions below 1, default the specified parameters to zero
            if (_containerSize.x - xPositions < 1)
                xPositions = 0;
            if (_containerSize.y - yPositions < 1)
                yPositions = 0;

            //return if no reduction is necessary
            if (xPositions == 0 && yPositions == 0)
                return;


            //calculate all of the target indexes 
            List<(int,int)> removalIndexes = new List<(int,int)> ();
            int xStartingRemovalIndex = _containerSize.x - xPositions -1;
            int yStartingRemovalIndex = _containerSize.y - yPositions -1;

            for (int y = 0; y < _containerSize.y; y++)
            {
                for (int x = 0; x < _containerSize.x; x++)
                {

                    if (x > xStartingRemovalIndex || y > yStartingRemovalIndex)
                    {
                        removalIndexes.Add((x, y));
                        Debug.Log($"index ({x},{y}) marked for removal");
                    }
                }
            }


            //ensure nothing exists within the specified reduction boundaries
            for (int i = 0; i < removalIndexes.Count; i++)
            {
                if (IsCellOccupied(removalIndexes[i]))
                {
                    Debug.LogWarning($"Attempted to reduce the grid size of contianer {gameObject.name} by ({xPositions},{yPositions}), " +
                        $"but Cell ({removalIndexes[i].Item1},{removalIndexes[i].Item2}) is occupied. Aborting grid Reduction.");
                    return;
                }
            }


            //now remove all of the specified indexes. Work from the back to the front.
            for (int i = removalIndexes.Count - 1; i >= 0; i--)
            {
                GameObject CellToRemove = _cellInteractCollection[removalIndexes[i]].gameObject;
                _cellInteractCollection.Remove(removalIndexes[i]);
                Destroy(CellToRemove);
            }

            _containerSize.x = _containerSize.x - xPositions;
            _containerSize.y = _containerSize.y - yPositions;

            //resize the window, too.
            ResizeContainer();

            if (_textUpdater == null)
            {
                _textUpdater = RepositionTextAtEndOfFrame();
                StartCoroutine(_textUpdater);
            }
            else
            {
                StopCoroutine(_textUpdater);
                _textUpdater = RepositionTextAtEndOfFrame();
                StartCoroutine(_textUpdater);
            }

            if (!suppressResizeEvent)
                OnGridResized?.Invoke(_containerSize);

        }

        /// <summary>
        /// Resizes the grid to match the given dimensions. Dimesions less than zero will be deafulted to 1. Utilitizes 
        /// the 'Expand/Reduce Grid' methods internally.
        /// </summary>
        /// <param name="newContainerDimensions"></param>
        public void ResizeGrid(Vector2Int newContainerDimensions)
        {
            if (newContainerDimensions.x < 1)
            {
                Debug.LogWarning($"Attempted to resize container '{name}' width below 1. Defaulting to 1. ");
                newContainerDimensions.x = 1;

            }
            if (newContainerDimensions.y < 1)
            {
                Debug.LogWarning($"Attempted to resize container '{name}' height below 1. Defaulting to 1. ");
                newContainerDimensions.y = 1;
            }

            int widthResizeDirection = 0;
            if (newContainerDimensions.x < _containerSize.x)
                widthResizeDirection = -1;
            else if (newContainerDimensions.x > _containerSize.x)
                widthResizeDirection = 1;

            int heightResizeDirection = 0;
            if (newContainerDimensions.y < _containerSize.y)
                heightResizeDirection = -1;
            else if (newContainerDimensions.y > _containerSize.y)
                heightResizeDirection = 1;

            //reduce the width and...
            if (widthResizeDirection < 0)
            {
                //reduce both width and height
                if (heightResizeDirection < 0)
                    ReduceGrid(_containerSize.x - newContainerDimensions.x, _containerSize.y - newContainerDimensions.y);

                //reduce the width, expand the height
                else if (heightResizeDirection > 0)
                {
                    ReduceGrid(_containerSize.x - newContainerDimensions.x, 0,true);
                    ExpandGrid(0, newContainerDimensions.y - _containerSize.y);
                }

                //only reduce the width
                else
                {
                    ReduceGrid(_containerSize.x - newContainerDimensions.x, 0);
                }
            }

            //expand width and...
            else if (widthResizeDirection > 0)
            {
                //expand both width and height
                if (heightResizeDirection > 0)
                    ExpandGrid(newContainerDimensions.x - _containerSize.x, newContainerDimensions.y - _containerSize.y);

                //expand the width, reduce the height
                else if (heightResizeDirection < 0)
                {
                    ExpandGrid(newContainerDimensions.x - _containerSize.x, 0, true);
                    ReduceGrid(0, _containerSize.y - newContainerDimensions.y);
                }

                //only expand the width
                else
                {
                    ExpandGrid(newContainerDimensions.x - _containerSize.x, 0);
                }
            }

            //leave width alone but...
            else
            {
                //expand the height
                if (heightResizeDirection > 0)
                    ExpandGrid(0, newContainerDimensions.y - _containerSize.y);

                //reduce the height
                else if (heightResizeDirection < 0)
                {
                    ReduceGrid(0, _containerSize.y - newContainerDimensions.y);
                }

                //dont expand or reduce anything if the current container size is the same as the requested size
                //...
            }


        }

        /// <summary>
        /// Returns every item that exists within the grid. Does NOT return the size of each stack. 
        /// Use 'GetStackValue' on the first (or any) index of each of the returned keys to get each stack's size. 
        /// </summary>
        /// <returns>A collection of unique keyValue pairs where the key represents all the occupied positions 
        /// of a specific item. The value associated with each key is the itemdata itself</returns>
        /// 
        public Dictionary<HashSet<(int,int)>,ItemData> GetAllStacks()
        {
            //make a new collection to give away, in case something tries to change the reference from beyond this script
            Dictionary<HashSet<(int, int)>, ItemData> allItems = new Dictionary<HashSet<(int, int)>, ItemData>(HashSet<(int, int)>.CreateSetComparer());
            

            foreach(KeyValuePair<HashSet<(int,int)>,ItemData> entry in _stackItemDatas)
            {
                HashSet<(int, int)> tempHashSet = new();

                //make a new collection to give away, in case something tries to change the reference from beyond this script
                foreach ((int, int) position in entry.Key)
                    tempHashSet.Add(position);

                //Debug.Log($"Stack: {entry.Value.name}\n Positions: {StringifyPositions(entry.Key)}");
                allItems.Add(tempHashSet, entry.Value);
            }

            return allItems;
        }

        /// <summary>
        /// Checks the grid for an amount of items and returns true if the full amount exists.
        /// Also provides where each found instance occurs via a collection of stackPositions[itemAmount].
        /// Will not provide the "detectedArea" result if the operation fails.
        /// </summary>
        /// <param name="item">The item to find</param>
        /// <param name="amount">The amount that needs to exist</param>
        /// <param name="detectedArea">The stackPositions of all found items, assuming the operation succeeded</param>
        /// <returns></returns>
        public bool DoesItemExist(ItemData item, int amount, out Dictionary<HashSet<(int,int)>,int> detectedArea)
        {
            detectedArea = new Dictionary<HashSet<(int, int)>,int>();
            
            if (item == null)
                return false;

            amount = Mathf.Max(1, amount);

            int neededAmount = amount;
            int amountTaken;

            foreach(KeyValuePair<HashSet<(int,int)>,ItemData> itemEntry in _stackItemDatas)
            {
                if (itemEntry.Value == item)
                {
                    //take exacly what we need if its all here
                    if ( _stackCapacities[itemEntry.Key] >= neededAmount)
                        amountTaken = neededAmount;

                    //or take the entire stack if we still need more
                    else
                        amountTaken = _stackCapacities[itemEntry.Key];

                    neededAmount -= amountTaken;
                    detectedArea.Add(itemEntry.Key, amountTaken);

                    if (neededAmount == 0)
                    {
                        //the exacct positions and amounts are provided by the out parameter
                        return true;
                    }
                }
            }
            
            //the requested amount wasn't found. Return false
            //Also clear the out parameter. Dont provide an incomplete query to the user
            detectedArea.Clear();
            return false;

        }
        
        /// <summary>
        /// Counts how many instances of a given item exist in the grid, though doesn't provide the found items' locations.
        /// </summary>
        /// <param name="itemToCount">the item that needs to be counted</param>
        /// <returns>The number of occurances of the specified item</returns>
        public int CountItem(ItemData itemToCount)
        {
            int count = 0;
            foreach (KeyValuePair<HashSet<(int,int)>,ItemData> entry in _stackItemDatas)
            {
                if (itemToCount == entry.Value)
                    count += _stackCapacities[entry.Key];
            }

            return count;
        }

        /// <summary>
        /// Returns true if this invGrid contains no items.
        /// </summary>
        /// <returns>true if no items exist in the grid. false otherwise.</returns>
        public bool IsEmpty()
        {
            Debug.Log($"number of stacks that exist in grid: {GetAllStacks().Count()}");
            return GetAllStacks().Count() == 0;
        }

        /// <summary>
        /// Places a give uiObject's origin over a specified cell position. 
        /// May also set the uiObject's sizeDelta to match the grid's cell size, if fitToCellSize is true.
        /// fitToCellSize is false by default.
        /// </summary>
        /// <param name="overlayPosition">What cell position should the provided object's origin be placed</param>
        /// <param name="overlayObject">The RectTranform of the uiObject to palce</param>
        /// <param name="fitToCellSize">Should the uiObject be resized to the grid's cell dimensions?</param>
        /// <param name="containerLayer">What grid container should the item be placed?</param>

        public void OverlayRectTransformOntoGrid(RectTransform overlayObject, RectTransform containerLayer, (int,int) overlayPosition, bool fitToCellSize = false)
        {
            if (containerLayer == null)
            {
                Debug.LogWarning("Attempted to apply an overlay to a NULL containerLayer. Ignoring Overlay request.");
                return;
            }

            if (overlayObject == null)
            {
                Debug.LogWarning("Cant overlay a NULL item onto the grid. Ignoring Overlay request.");
                return;
            }

            if (!IsCellOnGrid(overlayPosition))
            {
                Debug.LogWarning("Attempted to overlay a graphic onto a position that doesn't exist on the grid. This is technically possible, but restricted." +
                    " Ignoring Overlay Request.");
                return;
            }

            if (containerLayer == _unusedStackTextsContainer)
            {
                Debug.LogWarning("Attempted to add an item to the 'unused text objects' container. Don't do this. That container is" +
                    " dedicated to deactivated TMPro objects only, and is managed internally. Create a new container for yor own purposes instead.");
                return;
            }

            if (containerLayer == GetComponent<RectTransform>())
            {
                Debug.LogWarning("Attempted to add an item to the 'Grid' container. DO NOT DO THIS! This container is" +
                    " extremely child-order sensitive and is managed internally. Create a new container for yor own purposes instead.");
                return;
            }
            Vector3 containerPosition = containerLayer.position;

            //reparent the uiObject onto the grid visually
            //Get the position of the hovered cell, local to the grid
            Vector3 cellPosition = GetCellObject(overlayPosition).GetComponent<RectTransform>().localPosition;


            //parent the object to the grid's overlay container
            overlayObject.SetParent(containerLayer.GetComponent<Transform>(),false);
            overlayObject.localPosition = cellPosition;

            //ensure the graphic fits the cell's size
            if (fitToCellSize)
                overlayObject.sizeDelta = new Vector2(CellSize().x, CellSize().y);

        }
        public GridHoverVisualizer GetHoverVisualizer() { return _hoverVisualizer; }

        //Debug
        /// <summary>
        /// Builds a readable string from a hashset of (int,int).
        /// </summary>
        /// <param name="positions">The Set of positions to make readable</param>
        /// <returns>A readable string of (int,int)'s</returns>
        public static string StringifyPositions(HashSet<(int, int)> positions)
        {
            string log = "";
            foreach (var position in positions)
                log += position.ToString() + " ";
            return log;
        }

        private void ListenForDebugCommands()
        {
            if (_cmdFindSpaceForItem)
            {
                _cmdFindSpaceForItem = false;

                if (_paramItemData == null)
                    Debug.LogWarning("No paramItemData set. Ignoring FindSpace debug command.");
                else
                {
                    //reformat the param into the proper datatype
                    HashSet<(int, int)> excludeList = new HashSet<(int, int)>();
                    foreach (Vector2Int position in _settingExcludePositionsList)
                        excludeList.Add((position.x, position.y));

                    //Debug.Log($"Reformatted paramExcludePositionsList into hashset with {excludeList.Count} positions");
                    Debug.Log($"DoesSpaceExist for {_paramValue} item[{_paramItemData.name}]: {DoesSpaceExist(_paramItemData, _paramValue, excludeList)}");
                }
            }
            if (_cmdFindSpaceForAll)
            {
                _cmdFindSpaceForAll = false;
                Debug.Log($"Does Space Exist for List of queries: {DoesSpaceExist(_paramItemQueryList)}");
            }
            if (_cmdExpandGrid)
            {
                _cmdExpandGrid = false;
                ExpandGrid(_paramColumnRowAdjustment.x, _paramColumnRowAdjustment.y);
            }
            if (_cmdReduceGrid)
            {
                _cmdReduceGrid = false;
                ReduceGrid(_paramColumnRowAdjustment.x, _paramColumnRowAdjustment.y);
            }
            if (_cmdResizeGrid)
            {
                _cmdResizeGrid = false;
                ResizeGrid(_paramColumnRowAdjustment);
            }

            if (_cmdAddItem)
            {
                _cmdAddItem = false;
                AddItem(_paramItemData, _paramValue);
            }
            if (_cmdRemoveItem)
            {
                _cmdRemoveItem = false;
                RemoveItem(_paramItemData, _paramValue);
            }
            if (_cmdQueryPosition)
            {
                _cmdQueryPosition = false;

                (int, int) debugIndex = (_paramPosition.x, _paramPosition.y);

                //Log the miss if Off-grid was requested
                if (!IsCellOnGrid(debugIndex))
                    Debug.Log($"Position ({debugIndex.Item1},{debugIndex.Item2}) isn't on grid");

                //Log either [empty] or [ itemName : count ]
                else
                {
                    string debugString = $"Position ({debugIndex.Item1},{debugIndex.Item2}) => ";
                    if (IsCellOccupied(debugIndex))
                        debugString += $"[ {GetStackItemData(debugIndex).Name()} : {GetStackValue(debugIndex)} ]";
                    else
                        debugString += "[empty]";
                    Debug.Log(debugString);
                }
            }
            if (_cmdCountItem)
            {
                _cmdCountItem = false;
                if (_paramItemData == null)
                    Debug.LogWarning("No paramItemData set to count. Ignoring CountItem debug command.");
                else
                    Debug.Log($"{_paramItemData.Name()}(s) in grid: {CountItem(_paramItemData)}");
            }
        }
    }
}

