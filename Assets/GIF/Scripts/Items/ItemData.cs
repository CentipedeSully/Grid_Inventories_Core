using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace GIF
{

    [CreateAssetMenu(fileName = "ItemData")]
    public class ItemData : ScriptableObject
    {
        [Header("Item Definition")]
        [Tooltip("The in-game name of this item")]
        [SerializeField] private string _name;
        [Tooltip("The unique key to used to reference any instance of this item")]
        [SerializeField] private string _itemCode;
        [Tooltip("This item's size and shape, defined by a collection of cells")]
        [SerializeField] private List<Vector2Int> _spacialDefinition = new();
        [Tooltip("Which cell is considered the pivot point of this item's graphic")]
        [SerializeField] private Vector2Int _itemHandle;
        [Tooltip("The in-game description text of this item")]
        [SerializeField] private string _desc = "";
        [SerializeField] private Sprite _sprite;
        [Tooltip("The maximum amount of [this] item that can occupy the same grid positions")]
        [SerializeField] private int _stackLimit = 1;
        [Tooltip("The default trade value of this item")]
        [SerializeField] private int _value;

        [Header("Audio Cues")]
        [Tooltip("What sound will play when the item is held on the pointer/grid cell")]
        [SerializeField] private AudioClip _onPickUpAudio;
        [Tooltip("What sound will play when the item is placed into another cell")]
        [SerializeField] private AudioClip _onDropAudio;

        /*
        [Header("Possible UI Options")]
        [Tooltip("Should this item show the 'use' context within the inventory")]
        [SerializeField] private bool _isUsable;
        [Tooltip("Determines if more than one item may be used at a time")]
        [SerializeField] private bool _isBulkUseEnabled = false;
        [Tooltip("Should this item show the 'discard' context within the inventory")]
        [SerializeField] private bool _isDiscardable;
        [Tooltip("Should this item show the 'organize' context within the inventory. Controls whether or not the item can be manipulated")]
        [SerializeField] private bool _isOrganizable;
        [Tooltip("Should this item show the 'sell' context within the inventory")]
        [SerializeField] private bool _isSellable;
        */





        public static HashSet<(int,int)> CalculateClockWiseRotation(HashSet<(int,int)> indexes)
        {
            HashSet<(int, int)> newIndexes = new();
            foreach ((int,int) index in indexes)
            {
                (int, int) newIndex = (index.Item2, -index.Item1);
                newIndexes.Add(newIndex);
            }

            return newIndexes;

            

        }

        public static HashSet<(int,int)> CalculateCounterClockwiseRotation(HashSet<(int, int)> indexes)
        {
            HashSet<(int, int)> newIndexes = new();
            foreach ((int, int) index in indexes)
            {
                (int, int) newIndex = (-index.Item2, index.Item1);
                newIndexes.Add(newIndex);
            }

            return newIndexes;
        }

        /// <summary>
        /// Calculates what should be paid for the item(s) in question.
        /// </summary>
        /// <param name="item">The item[Data] in question</param>
        /// <param name="amount">The amount of items to charge for</param>
        /// <param name="priceMultiplier">The seller's markup/discount percentage</param>
        /// <returns>An int (rounded up) that's the calculated price of all the items</returns>
        public static int CalculatePrice(ItemData item, int amount, float priceMultiplier)
        {
            if (item == null)
                return 0;

            //make sure the merhcant isn't ripping off the player via rounding errors ^_^
            int individualItemSale = (int)Mathf.Ceil(item.ItemValue() * priceMultiplier);

            return individualItemSale * amount;
        }

        public string Name() { return _name; }
        public HashSet<(int, int)> SpacialDefinition()
        {
            //Convert the vector2Int types into tuples
            //tuples aren't serialized in the inspector, but they're faster to type on the keyboard XD
            HashSet<(int, int)> spacialDefTuples = new();

            foreach (Vector2Int index in _spacialDefinition)
                spacialDefTuples.Add((index.x, index.y));

            //Debug.Log($"Tracing Item Creation:\nSpacialDef size: {spacialDefTuples.Count}");
            return spacialDefTuples;
        }
        public (int, int) ItemHandle() { return (_itemHandle.x, _itemHandle.y); }
        public string Desc() { return _desc; }
        public Sprite Sprite() { return _sprite; }
        public int StackLimit() { return _stackLimit; }
        public string ItemCode() { return _itemCode; }
        public HashSet<(int,int)> RotatedSpacialDef(ItemRotation desiredRotation)
        {
            HashSet<(int,int)> rotatedIndexes = new();

            switch (desiredRotation)
            {
                case ItemRotation.None:
                    rotatedIndexes = SpacialDefinition();
                    break;

                case ItemRotation.Once:
                    rotatedIndexes = CalculateClockWiseRotation(SpacialDefinition());
                    break;

                case ItemRotation.Twice:
                    rotatedIndexes = CalculateClockWiseRotation(CalculateClockWiseRotation(SpacialDefinition()));
                    break;

                case ItemRotation.Thrice: // :)
                    rotatedIndexes = CalculateClockWiseRotation(CalculateClockWiseRotation(CalculateClockWiseRotation(SpacialDefinition())));
                    break;
            }

            return rotatedIndexes;
        }

        public (int,int) RotatedItemHandle(ItemRotation desiredRotation)
        {
            HashSet<(int, int)> handleHash = new()
            {
                ItemHandle()
            };

            switch (desiredRotation)
            {
                case ItemRotation.None:
                    break;

                case ItemRotation.Once:
                    handleHash = CalculateClockWiseRotation(handleHash);
                    break;

                case ItemRotation.Twice:
                    handleHash = CalculateClockWiseRotation(CalculateClockWiseRotation(handleHash));
                    break;

                case ItemRotation.Thrice: // :)
                    handleHash = CalculateClockWiseRotation(CalculateClockWiseRotation(CalculateClockWiseRotation(handleHash)));
                    break;
            }

            return handleHash.First();
        }

        public AudioClip OnPickupAudioClip() { return _onPickUpAudio; }
        public AudioClip OnDropAudioClip() { return _onDropAudio; }
        //public bool IsBulkUseEnabled() { return _isBulkUseEnabled; }
        //public bool IsSellable() {  return _isSellable; }
        public int ItemValue() { return _value; }
    }

    

}

