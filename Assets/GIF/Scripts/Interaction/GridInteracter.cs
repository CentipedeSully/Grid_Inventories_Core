using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TMPro.SpriteAssetUtilities;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

namespace GIF
{
    public class GridInteracter : MonoBehaviour
    {
        //Declarations
        [Header("References")]
        [Tooltip("Where pinned stacks/items will live. This object will follow the mouse (when applicable)")]
        [SerializeField] private RectTransform _pointerContainer;
        [SerializeField] private TextMeshProUGUI _pinnedText;
        [SerializeField] private GridHoverVisualizer _hoverVisualizer;
        [SerializeField] private AudioSource _audioSource;

        private Vector3 _mousePosition;

        [Header("Settings")]
        [SerializeField] public float _audioVolume = 1.0f;
        [Tooltip("This plays whenever the pointer enters a new cell. Its multiplied by 'Audio Volume' above. This is separated because its likely " +
            "this sound'll play VERY frequently. Try setting this value low to avoid annoying the user.")]
        [SerializeField] public float _cellMovementVolume = 1.0f;
        [SerializeField] private AudioClip _cellMovementAudio;
        [SerializeField] private AudioClip _rotateAudio;

        [Header("Pinned Item")]
        [SerializeField] private RectTransform _pinnedRectTransform;
        [SerializeField] private InvItem _pinnedItem;
        [SerializeField] private int _pinnedAmount;
        private Vector2 _tileSize = Vector2.zero;
        private HashSet<(int, int)> _pinnedItemHoverArea;

        [Header("Watch States (Don't Touch)")]
        [SerializeField] private InvGrid _hoveredGrid;
        [SerializeField] private CellInteract _hoveredCell;
        [SerializeField] private Vector2 _hoveredCellPosition = -Vector2.one;
        [SerializeField] private InvItem _hoveredInvItem;

        [Header("Debug")]
        [SerializeField] private bool _logDetectedGridEvents = false;




        //monobehaviours
        private void Awake()
        {
            InteracterHelper.SetInteracter(this);
        }

        private void Update()
        {
            if (_pointerContainer != null)
                BindPointerContainerToCurrentMousePosition();
        }


        //internals
        private void BindPointerContainerToCurrentMousePosition()
        {
            _pointerContainer.position = _mousePosition;
        }

        private void PinInvItem(InvItem item, int amount, InvGrid grid)
        {

            if (item == null)
                return;

            if (amount == 0)
                return;

            if (_pointerContainer == null)
            {
                Debug.LogWarning("Cant pin items/stacks until a pointerContainer exists. Ignoring pin command.");
                return;
            }

            if (_pinnedItem != null)
            {
                Debug.LogWarning("Can't pin more than 1 item at a time. Ignoring pin request.");
                return;
            }

            else 
            {
                InvItem newItem = ItemCreatorHelper.CreateItem(item.ItemData(), grid.CellSize().x, grid.CellSize().y).GetComponent<InvItem>();
                int rotationsChecked = 0;
                //match the rotation of the reference item
                //autoBreak if we rotated the item 20 times and still havent found the right rotation.
                while (newItem.Rotation() != item.Rotation() && rotationsChecked < 20)
                {
                    newItem.RotateItem(RotationDirection.Clockwise);
                    rotationsChecked++;
                }

                //tell the dev how and why the process failed. Give feedback
                if (rotationsChecked >= 20 && newItem.Rotation() != item.Rotation())
                {
                    Debug.LogWarning($"Failed to infer the original item's current rotation [{item.Rotation()}] within {rotationsChecked} rotations. " +
                        $"You need to either define your rotations in a cyclical, looped fashion, or ahve less of them. Alternatively you can increase" +
                        $"the amount of rotations checked here in the code [above where this warning was generated], assuming all of your rotations are reachable.");

                    ItemCreatorHelper.ReturnItemToCreator(newItem);
                    return;
                }


                //set the item as pinned
                _pinnedItem = newItem;
                _pinnedRectTransform = _pinnedItem.GetComponent<RectTransform>();
                _pinnedAmount = amount;

                //pin the item to the pointer container
                _pinnedRectTransform.SetParent(_pointerContainer);
                _pinnedRectTransform.localPosition = Vector2.zero;

                return;
            }

            



        }

        private void ClearPinnedAmount(int removalAmount)
        {
            if (_pinnedItem == null)
                return;

            //if we aren't dropping the entire stack, then just reduce the pinned amount by the removal amount 
            if (_pinnedAmount > removalAmount)
            {
                _pinnedAmount -= removalAmount;
            }

            //otherwise return everything to the item creator
            else
            {
                _pinnedAmount = 0;
                _pinnedRectTransform = null;
                ItemCreatorHelper.ReturnItemToCreator(_pinnedItem);
                _pinnedItem = null;

                
            }
        }

        private void UpdatePinnedStackText()
        {
            if (_pinnedText != null)
            {
                _pinnedText.text = _pinnedAmount.ToString();

                if (_pinnedAmount <= 1)
                    _pinnedText.gameObject.SetActive(false);
                else
                    _pinnedText.gameObject.SetActive(true);

                _pinnedText.transform.SetAsLastSibling();
            }
        }

        private void UpdateFromDetectedChanges(InvContentsUpdate update)
        {
            if (_hoverVisualizer != null)
                _hoverVisualizer.ClearAllHoveredCells();

            //for each position that was updated, if our position was affected, then update the hovered item state
            foreach(HashSet<(int,int)> stackArea in update.stackAreasAffected)
            {
                if (stackArea.Contains(_hoveredCell.Index()))
                {
                    _hoveredInvItem = _hoveredCell.Item();
                    break;
                }
            }

            DrawHoverEffects();
        }

        private void DrawHoverEffects()
        {
            if (_hoverVisualizer != null)
            {
                //do we NOT have an item currently pinned?
                if (_pinnedItem == null)
                {
                    //if we aren't hovering over an item, then only show the hover effect at our currend hovered position
                    if (_hoveredInvItem == null)
                        _hoverVisualizer.CreateHoverOnCell(_hoveredCell.Index());
                    
                    //otherwise, show a hover effect over each of the detected item's grid occupancy
                    else
                    {
                        foreach ((int, int) position in _hoveredGrid.GetStackArea(_hoveredCell.Index()))
                            _hoverVisualizer.CreateHoverOnCell(position);
                    }
                }

                //an item is pinned. The default hover effects should reflect the item's possible placement position
                else
                {
                    //calculate the cells our pinned item is hovering over
                    _pinnedItemHoverArea = _hoveredGrid.ConvertSpacialDefIntoGridArea(_hoveredCell.Index(), _pinnedItem.GetSpacialDefinition(), _pinnedItem.ItemHandle());

                    //only show hover effects if every cell is within the grid
                    if (_hoveredGrid.IsAreaWithinGrid(_pinnedItemHoverArea))
                    {
                        foreach ((int, int) position in _pinnedItemHoverArea)
                            _hoverVisualizer.CreateHoverOnCell(position);
                    }
                }
            }
        }

        private void ClearHoverEffects()
        {
            if (_hoverVisualizer != null)
            {
                _hoverVisualizer.ClearAllHoveredCells();
            }
        }

        private void PlayAudio(AudioClip clip, bool isCellMovementAudio = false)
        {
            if (clip == null)
            {
                Debug.LogWarning("Attempted to play a null audio clip. Ignoring PlayAudio Request.");
                return;
            }

            if (_audioSource == null)
            {
                Debug.LogWarning("Attempted to play an audioClip while no audioSource is set. Ignoring PlayAudio Request.");
                return;
            }

            if (!isCellMovementAudio)
                _audioSource.PlayOneShot(clip, _audioVolume);

            else
                _audioSource.PlayOneShot(clip, _audioVolume * _cellMovementVolume);
        }



        //externals
        public void SetMousePosition(Vector3 newPosition)
        { 
            _mousePosition = newPosition; 
        }
        public void SetHoveredGrid(InvGrid grid)
        {
            
            if (grid == null)
            {
                Debug.LogWarning("Attempted to set a null grid as hovered. Ignoring set request.");
                return;
            }

            if (grid == _hoveredGrid)
            {
                //Debug.LogWarning("grid Already set as hovered. Ignoring set request.");
                return;
            }

            _hoveredGrid = grid;
            _hoverVisualizer = grid.GetHoverVisualizer();
            SubscribeToEnteredGrid();

            //resize the pinned item, in case we just entered a grid of differently-sized cells
            if (_pinnedItem != null)
            {
                if (_tileSize != _hoveredGrid.CellSize())
                {

                    _tileSize = _hoveredGrid.CellSize();

                    //repin a new, resized item to the item container.
                    InvItem newItem = ItemCreatorHelper.CreateItem(_pinnedItem.ItemData(), _tileSize.x, _tileSize.y).GetComponent<InvItem>();

                    //rotate the new item to match the pinned item
                    int possibleRotations = Enum.GetValues(typeof(ItemRotation)).Length;
                    int rotationsAttempted = 0;
                    while (newItem.Rotation() != _pinnedItem.Rotation() && rotationsAttempted < possibleRotations)
                    {
                        newItem.RotateItem(RotationDirection.CounterClockwise);
                        rotationsAttempted++;
                    }

                    if (rotationsAttempted >= possibleRotations && newItem.Rotation() != _pinnedItem.Rotation())
                    {
                        Debug.LogWarning("Attempted to seamlessly switch items similar items (to resize the pinned item) due to a detected difference in" +
                            " tileSizes, but failed to rotate the newly-sized item to match the currently-pinned item. Ensure the rotations of your items are" +
                            " cyclic-- meaning your 'Rotate' implementation goes through all possible rotations in a cycle if called enough times. " +
                            "We're keeping the newly-created item, but the rotation will be different.");

                        
                    }

                    //return the old item to the item Creator
                    ItemCreatorHelper.ReturnItemToCreator(_pinnedItem);

                    //update the new item as the pinned item
                    _pinnedItem = newItem;
                    _pinnedRectTransform = newItem.GetComponent<RectTransform>();

                    //pin the item to the pointer container
                    _pinnedRectTransform.SetParent(_pointerContainer);
                    _pinnedRectTransform.localPosition = Vector2.zero;

                }
            }

            //always ensure this it updated anyway
            _tileSize = _hoveredGrid.CellSize();

            _pinnedText.GetComponent<RectTransform>().SetAsLastSibling();

            

            
        }
        public void ClearHoveredGrid()
        {
            
            if (_hoveredGrid != null)
            {
                
                UnsubscribeFromGrid();
                ClearHoverEffects();
                _hoveredGrid = null;
                _hoverVisualizer = null;
            }
        }
        public void SetHoveredCell(CellInteract cell)
        {
            
            if (cell == null)
            {
                Debug.LogWarning("Attempted to set a null cell as hovered. Ignoring set request.");
                return;
            }

            if (_hoveredCell != null)
                ClearHoveredCell();
           
            _hoveredCell = cell;
            _hoveredCellPosition = new Vector2(cell.Index().Item1, cell.Index().Item2);
            _hoveredInvItem = cell.Item();


            if (_hoveredGrid == null)
            {
                SetHoveredGrid(_hoveredCell.Grid());
            }
            DrawHoverEffects();

            PlayAudio(_cellMovementAudio, true);


        }
        public void ClearHoveredCell()
        {
            
            if (_hoveredCell != null)
            {
                ClearHoverEffects();

                _hoveredCell = null;
                _hoveredCellPosition = -Vector2.one;
                _hoveredInvItem = null;

                
            }
        }

        public void PinHoveredStackToMouse()
        {
            if (_hoveredGrid == null)
            {
                Debug.LogWarning("Can't pin a stack from a null grid.");
                return;
            }

            if (_hoveredCell == null)
            {
                Debug.LogWarning("Can't pin a stack from a null hovered cell.");
                return;
            }

            //don't pin a stack while already holding a pinned stack
            if (_pinnedItem != null && _hoveredInvItem != null)
            {
                Debug.LogWarning("Can't pin a stack while already holding an (incompatible) pinned stack.");
                return;
            }

            //if we're not holding a stack, pin what's hovered
            else if (_hoveredInvItem != null)
            {
                //pin the stack to the pointer
                PinInvItem(_hoveredInvItem, _hoveredGrid.GetStackValue(_hoveredCell.Index()), _hoveredGrid);
                UpdatePinnedStackText();

                //remove the stack from the grid
                _hoveredGrid.RemoveItem(_hoveredCell.Index(), _pinnedAmount);

                //play the pinned item's pickup audio
                PlayAudio(_pinnedItem.ItemData().OnPickupAudioClip());
                return;
            }
        }
        public void PinSingleFromHoveredStackToMouse()
        {
            if (_hoveredGrid == null)
            {
                Debug.LogWarning("Can't pin a stack from a null grid.");
                return;
            }

            if (_hoveredCell == null)
            {
                Debug.LogWarning("Can't pin a stack from a null hovered cell.");
                return;
            }

            //don't pin a stack while already holding a pinned stack
            if (_pinnedItem != null && _hoveredInvItem != null)
            {
                Debug.LogWarning("Can't pin a stack while already holding an (incompatible) pinned stack.");
                return;
            }

            //if we're not holding a stack, pin a single item from the hovered stack
            else if (_hoveredInvItem != null)
            {
                //pin the stack to the pointer
                PinInvItem(_hoveredInvItem, 1, _hoveredGrid);
                UpdatePinnedStackText();

                //remove the stack from the grid
                _hoveredGrid.RemoveItem(_hoveredCell.Index(), _pinnedAmount);

                //play the pinned item's pickup audio
                PlayAudio(_pinnedItem.ItemData().OnPickupAudioClip());
                return;
            }
        }
        public void PinHalfFromHoveredStackToMouse()
        {
            if (_hoveredGrid == null)
            {
                Debug.LogWarning("Can't pin a stack from a null grid.");
                return;
            }

            if (_hoveredCell == null)
            {
                Debug.LogWarning("Can't pin a stack from a null hovered cell.");
                return;
            }

            //don't pin a stack while already holding a pinned stack
            if (_pinnedItem != null && _hoveredInvItem != null)
            {
                Debug.LogWarning("Can't pin a stack while already holding an (incompatible) pinned stack.");
                return;
            }

            //if we're not holding a stack, pin what's hovered
            else if (_hoveredInvItem != null)
            {
                int hoveredStackSize = _hoveredGrid.GetStackValue(_hoveredCell.Index());
                int amountToPin;

                if (hoveredStackSize == 1)
                    amountToPin = 1;
                else
                    amountToPin = Mathf.CeilToInt(hoveredStackSize/ 2.0f); //ensure we round up!

                //pin the stack to the pointer
                PinInvItem(_hoveredInvItem, amountToPin, _hoveredGrid);
                UpdatePinnedStackText();

                //remove the stack from the grid
                _hoveredGrid.RemoveItem(_hoveredCell.Index(), _pinnedAmount);

                //play the pinned item's pickup audio
                PlayAudio(_pinnedItem.ItemData().OnPickupAudioClip());
                return;
            }
        }

        public void PlacePinnedStackToHoveredCell()
        {
            if (_hoveredGrid == null)
            {
                Debug.LogWarning("Can't place a stack to a null grid.");
                return;
            }

            if (_hoveredCell == null)
            {
                Debug.LogWarning("Can't palce a stack to a null hovered cell.");
                return;
            }

            if (_pinnedItem != null)
            {
                HashSet<(int, int)> placementArea = _hoveredGrid.ConvertSpacialDefIntoGridArea(_hoveredCell.Index(), _pinnedItem.GetSpacialDefinition(), _pinnedItem.ItemHandle());
                //Debug.Log($"Drop Stack Called. Placement area: {_invGrid.StringifyPositions(placementArea)}");
                //make sure the entire item area is within the grid
                if (_hoveredGrid.IsAreaWithinGrid(placementArea))
                {
                    int itemCount = _hoveredGrid.CountUniqueStacksInArea(placementArea);

                    //if position is completely empty, place here
                    if (itemCount == 0)
                    {
                        //play the pinned item's drop audio
                        PlayAudio(_pinnedItem.ItemData().OnDropAudioClip());

                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), _pinnedAmount, _hoveredCell.Index(), _pinnedItem.Rotation());
                        ClearPinnedAmount(_pinnedAmount);
                        UpdatePinnedStackText();

                        

                        return;
                    }

                    //if only one stack found, top off the stack if compatible and available, or swap stacks otherwise
                    else if (itemCount == 1)
                    {
                        foreach ((int, int) index in placementArea)
                        {
                            //find the first cell that our detected stack is occupying
                            if (_hoveredGrid.IsCellOccupied(index))
                            {
                                //if the stack is compatible and not yet full, top it off
                                if (_hoveredGrid.GetStackItemData(index).ItemCode() == _pinnedItem.ItemData().ItemCode() && _hoveredGrid.GetStackValue(index) < _pinnedItem.ItemData().StackLimit())
                                {
                                    int stackValue = _hoveredGrid.GetStackValue(index);
                                    int stackMaxCapacity = _hoveredGrid.GetStackItemData(index).StackLimit();
                                    int openCapacity = stackMaxCapacity - stackValue; //openCapacity will always be above zero if we've made it this far

                                    //place all held items here if the stack can take it
                                    if (_pinnedAmount <= openCapacity)
                                    {
                                        //play the pinned item's drop audio
                                        PlayAudio(_pinnedItem.ItemData().OnDropAudioClip());

                                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), _pinnedAmount, index, _pinnedItem.Rotation());
                                        ClearPinnedAmount(_pinnedAmount);

                                        UpdatePinnedStackText();

                                        return;
                                    }

                                    //otherwise, only place enough items to fill the stack here. Don't clear the held item yet, since we have some left.
                                    else
                                    {
                                        //play the pinned item's drop audio
                                        PlayAudio(_pinnedItem.ItemData().OnDropAudioClip());

                                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), openCapacity, index, _pinnedItem.Rotation());
                                        ClearPinnedAmount(openCapacity);
                                        UpdatePinnedStackText();

                                        return;
                                    }

                                }


                                //otherwise, swap the stacks
                                else
                                {
                                    //play the pinned item's drop audio
                                    PlayAudio(_pinnedItem.ItemData().OnDropAudioClip());

                                    //save the found item's data
                                    InvItem newGraphic = _hoveredGrid.GetInvItemOnCell(index);
                                    int stackSize = _hoveredGrid.GetStackValue(index);

                                    //delete the currently-stored item
                                    _hoveredGrid.RemoveItem(index, stackSize);

                                    //place the held item into the now-fully-open position
                                    _hoveredGrid.AddItem(_pinnedItem.ItemData(), _pinnedAmount, _hoveredCell.Index(), _pinnedItem.Rotation());

                                    ClearPinnedAmount(_pinnedAmount);

                                    //update our held itemData
                                    PinInvItem(newGraphic, stackSize, _hoveredGrid);
                                    UpdatePinnedStackText();

                                    return;
                                }
                            }
                        }

                        /* If here was reached, then no stacks were found.
                         * This shouldn't ever happen, since we SUPPOSEDLY found exactly
                         * 1 preexisting stack before we entered this block. 
                         * Raise a red error. Something's wrong with our stack lookUp/creation
                         */
                        Debug.LogError($"Error during itemSwaping via InvManager: couldn't find the inventory stack that should definitely exist." +
                            $" There's probably an error with itemStack lookup or item stack creation within the InvGrid, OR the InvManager isn't" +
                            $" looking where it should be (mixed up indexes? Wrong parameter?).");

                    }

                    //if many stacks are being hovered over, top off as many compatible stacks as possible. Don't attempt to swap stacks.
                    else if (itemCount  > 1)
                    {
                        foreach ((int, int) index in placementArea)
                        {
                            //find the first cell that our detected stack is occupying
                            if (_hoveredGrid.IsCellOccupied(index))
                            {
                                //if the stack is compatible and not yet full, top it off
                                if (_hoveredGrid.GetStackItemData(index).ItemCode() == _pinnedItem.ItemData().ItemCode() && _hoveredGrid.GetStackValue(index) < _pinnedItem.ItemData().StackLimit())
                                {
                                    int stackValue = _hoveredGrid.GetStackValue(index);
                                    int stackMaxCapacity = _hoveredGrid.GetStackItemData(index).StackLimit();
                                    int openCapacity = stackMaxCapacity - stackValue; //openCapacity will always be above zero if we've made it this far

                                    //place all held items here if the stack can take it
                                    if (_pinnedAmount <= openCapacity)
                                    {
                                        //play the pinned item's drop audio
                                        PlayAudio(_pinnedItem.ItemData().OnDropAudioClip());

                                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), _pinnedAmount, index, _pinnedItem.Rotation());
                                        ClearPinnedAmount(_pinnedAmount);

                                        UpdatePinnedStackText();

                                        //we placed everything. We can return.
                                        return;
                                    }

                                    //otherwise, only place enough items to fill the stack here. Don't clear the held item yet, since we have some left.
                                    else
                                    {

                                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), openCapacity, index, _pinnedItem.Rotation());
                                        ClearPinnedAmount(openCapacity);
                                        UpdatePinnedStackText();

                                        //don't return. Keep looking for open stacks.

                                    }

                                }
                            }
                        }

                    }
                }
            }

        }
        public void PlaceSingleToHoveredCell()
        {
            if (_hoveredGrid == null)
            {
                Debug.LogWarning("Can't place a stack to a null grid.");
                return;
            }

            if (_hoveredCell == null)
            {
                Debug.LogWarning("Can't palce a stack to a null hovered cell.");
                return;
            }

            if (_pinnedItem != null)
            {
                HashSet<(int, int)> placementArea = _hoveredGrid.ConvertSpacialDefIntoGridArea(_hoveredCell.Index(), _pinnedItem.GetSpacialDefinition(), _pinnedItem.ItemHandle());
                //Debug.Log($"Drop Single item Called. Placement area: {_invGrid.StringifyPositions(placementArea)}");
                //make sure the entire item area is within the grid
                if (_hoveredGrid.IsAreaWithinGrid(placementArea))
                {
                    int itemCount = _hoveredGrid.CountUniqueStacksInArea(placementArea);

                    //if position is completely empty, place here
                    if (itemCount == 0)
                    {

                        //PlayItemDropAudio();
                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), 1, _hoveredCell.Index(), _pinnedItem.Rotation());
                        ClearPinnedAmount(1);
                        UpdatePinnedStackText();
                        return;
                    }

                    //if one or more stacks are found, top off the first compatible stack thats detected. Do not swap stacks.
                    else if (itemCount >= 1)
                    {
                        foreach ((int, int) index in placementArea)
                        {
                            //find the first cell that our detected stack is occupying
                            if (_hoveredGrid.IsCellOccupied(index))
                            {
                                //if the stack is compatible and not yet full, top it off
                                if (_hoveredGrid.GetStackItemData(index).ItemCode() == _pinnedItem.ItemData().ItemCode() && _hoveredGrid.GetStackValue(index) < _pinnedItem.ItemData().StackLimit())
                                {
                                    int stackValue = _hoveredGrid.GetStackValue(index);
                                    int stackMaxCapacity = _hoveredGrid.GetStackItemData(index).StackLimit();
                                    int openCapacity = stackMaxCapacity - stackValue; //openCapacity will always be above zero if we've made it this far

                                    //place the one pinned items here if the stack can take it
                                    if (openCapacity > 0)
                                    {
                                        //PlayItemDropAudio();
                                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), 1, index, _pinnedItem.Rotation());
                                        ClearPinnedAmount(1);

                                        UpdatePinnedStackText();

                                        return;
                                    }

                                }
                            }
                        }
                    }
                }
            }
        }
        public void PlaceHalfToHoveredCell()
        {
            if (_hoveredGrid == null)
            {
                Debug.LogWarning("Can't place a stack to a null grid.");
                return;
            }

            if (_hoveredCell == null)
            {
                Debug.LogWarning("Can't palce a stack to a null hovered cell.");
                return;
            }

            if (_pinnedItem != null)
            {
                HashSet<(int, int)> placementArea = _hoveredGrid.ConvertSpacialDefIntoGridArea(_hoveredCell.Index(), _pinnedItem.GetSpacialDefinition(), _pinnedItem.ItemHandle());
                //Debug.Log($"Drop HalfStack Called. Placement area: {_invGrid.StringifyPositions(placementArea)}");
                //make sure the entire item area is within the grid
                if (_hoveredGrid.IsAreaWithinGrid(placementArea))
                {
                    int itemCount = _hoveredGrid.CountUniqueStacksInArea(placementArea);
                    int amountToPlace;

                    if (_pinnedAmount == 1)
                        amountToPlace = 1;
                    else
                        amountToPlace = Mathf.CeilToInt(_pinnedAmount / 2.0f); //ensure we round up!

                    //if position is completely empty, place here
                    if (itemCount == 0)
                    {

                        //PlayItemDropAudio();
                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), amountToPlace, _hoveredCell.Index(), _pinnedItem.Rotation());
                        ClearPinnedAmount(amountToPlace);
                        UpdatePinnedStackText();
                        return;
                    }

                    //if stacks were found, top off the all compatible stacks. Do not swap stacks.
                    else if (itemCount >= 1)
                    {
                        foreach ((int, int) index in placementArea)
                        {
                            //find the first cell that our detected stack is occupying
                            if (_hoveredGrid.IsCellOccupied(index))
                            {
                                //if the stack is compatible and not yet full, top it off
                                if (_hoveredGrid.GetStackItemData(index).ItemCode() == _pinnedItem.ItemData().ItemCode() && _hoveredGrid.GetStackValue(index) < _pinnedItem.ItemData().StackLimit())
                                {
                                    int stackValue = _hoveredGrid.GetStackValue(index);
                                    int stackMaxCapacity = _hoveredGrid.GetStackItemData(index).StackLimit();
                                    int openCapacity = stackMaxCapacity - stackValue; //openCapacity will always be above zero if we've made it this far

                                    //place all held items here if the stack can take it
                                    if (amountToPlace <= openCapacity)
                                    {
                                        //PlayItemDropAudio();
                                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), amountToPlace, index, _pinnedItem.Rotation());
                                        ClearPinnedAmount(amountToPlace);

                                        UpdatePinnedStackText();

                                        return;
                                    }

                                    //otherwise, only place enough items to fill the stack here. Don't clear the held item yet, since we have some left.
                                    else
                                    {
                                        //PlayItemDropAudio();
                                        _hoveredGrid.AddItem(_pinnedItem.ItemData(), openCapacity, index, _pinnedItem.Rotation());
                                        amountToPlace -= openCapacity;
                                        ClearPinnedAmount(amountToPlace);
                                        UpdatePinnedStackText();

                                    }

                                }
                            }
                        }

                    }
                }
            }
        }

        


        public void RotateClockwise()
        {
            if (_pinnedItem != null)
            {
                _pinnedItem.RotateItem(RotationDirection.Clockwise);
                ClearHoverEffects();
                DrawHoverEffects();
                PlayAudio(_rotateAudio);


            }
        }
        public void RotateCounterClockwise()
        {
            if (_pinnedItem != null)
            {
                _pinnedItem.RotateItem(RotationDirection.CounterClockwise);
                ClearHoverEffects(); 
                DrawHoverEffects();
                PlayAudio(_rotateAudio);
            }
        }

        public void SubscribeToEnteredGrid()
        {
            _hoveredGrid.OnContentsChanged += RespondToGridContentsUpdated;
            _hoveredGrid.OnBulkContentsChanged += RespondToBulkGridContentUpdate;
            _hoveredGrid.OnGridDestroyed += RespondToUnexpectedGridDeletion;
        }
        public void UnsubscribeFromGrid()
        {
            _hoveredGrid.OnContentsChanged -= RespondToGridContentsUpdated;
            _hoveredGrid.OnBulkContentsChanged -= RespondToBulkGridContentUpdate;
            _hoveredGrid.OnGridDestroyed -= RespondToUnexpectedGridDeletion;
        }
        public void RespondToGridContentsUpdated(InvContentsUpdate update)
        {
            if (_logDetectedGridEvents)
                LogGridOnChangedActivity(update);

            UpdateFromDetectedChanges(update);

        }
        public void RespondToBulkGridContentUpdate(List<InvContentsUpdate> updatesList)
        {
            if (_logDetectedGridEvents)
            {
                for (int i = 0; i < updatesList.Count; i++)
                    LogGridOnChangedActivity(updatesList[i]);
            }
            
        }
        public void RespondToUnexpectedGridDeletion()
        {
            
            Debug.LogWarning("Unexpected grid deletion detected. Unsubscribing from grid before deletion occurs...");
            UnsubscribeFromGrid();
            ClearHoveredCell();
            ClearHoveredGrid();
            
            Debug.LogWarning("Unsub successful. grindInteracter state updated.");
        }


        //debug

        private void LogGridOnChangedActivity(InvContentsUpdate update)
        {
            string debugString = $"Detected Grid activty: \nOperation : {update.operation}\nItem : {update.itemData}\nAmount : {update.amount}\nPositions Affected:\n";
            foreach (HashSet<(int,int)> stackArea in update.stackAreasAffected)
            {
                debugString += "{ " + InvGrid.StringifyPositions(stackArea) + " }\n";
            }
            Debug.Log(debugString);
        }
    }


    public static class InteracterHelper
    {
        private static GridInteracter _interacter;


        public static void SetInteracter(GridInteracter interacter)
        {
            if (interacter == null)
                return;

            _interacter = interacter;
        }

        public static void SetGridAsHovered(InvGrid grid)
        {
            if (grid == null)
                return;

            if (_interacter == null)
            {
                Debug.LogError("Attempted to set a grid as hovered for the GridInteracter, but the GridInteracter hasn't been set." +
                    " Ensure only a single GridInteracter exists in the scene; it'll set itself up onAwake");
                return;
            }

            _interacter.SetHoveredGrid(grid);
        }
        public static void ClearGrid()
        {
            if (_interacter == null)
            {
                Debug.LogError("Attempted to clear a grid from being hovered for the GridInteracter, but the GridInteracter hasn't been set." +
                    " Ensure only a single GridInteracter exists in the scene; it'll set itself up onAwake");
                return;
            }
            _interacter.ClearHoveredGrid();
        }

        public static void SetCellAsHovered(CellInteract cell)
        {
            if (cell == null)
                return;

            if (_interacter == null)
            {
                Debug.LogError("Attempted to set a cell as hovered for the GridInteracter, but the GridInteracter hasn't been set." +
                    " Ensure only a single GridInteracter exists in the scene; it'll set itself up onAwake");
                return;
            }

            _interacter.SetHoveredCell(cell);
        }
        public static void ClearCell()
        {
            if (_interacter == null)
            {
                Debug.LogError("Attempted to clear a cell from being hovered for the GridInteracter, but the GridInteracter hasn't been set." +
                    " Ensure only a single GridInteracter exists in the scene; it'll set itself up onAwake");
                return;
            }
            _interacter.ClearHoveredCell();
        }
    }

}