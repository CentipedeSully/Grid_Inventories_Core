using GIF;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GIF
{
    [Serializable]
    /// <summary>
    /// Basically the InvContentsUpdate struct, but with a list of vectors instead of tuples.
    /// Used exclusively to help debug via inspector (Unity doesn't serialize tuples).
    /// This struct will be converted into the unserialized version when used internally.
    /// </summary>
    public struct SerializableInvContentsUpdate
    {
        public InvOperation operation;
        public ItemData itemData;
        public int amount;


        
        public List<InnerList> stackAreasAffected;

        public SerializableInvContentsUpdate(ItemData itemTypechanged, int changeAmount, InvOperation operationThatHappened, List<InnerList> stackAreasChanged)
        {
            this.operation = operationThatHappened;
            this.itemData = itemTypechanged;
            this.amount = changeAmount;

            this.stackAreasAffected = new();

            foreach (InnerList element in stackAreasChanged)
            {
                stackAreasAffected.Add(element);
            }
        }
    }

    [Serializable]
    /// <summary>
    /// Literally just a list of Vector2Ints. Did this to get around being unable to serialize lists of lists ;3
    /// </summary>
    public struct InnerList
    {
        public List<Vector2Int> list;

        public InnerList(List<Vector2Int> listRef)
        {
            list = new();
            for (int i = 0; i < listRef.Count; i++)
                list.Add(listRef[i]);
        }
    }

    public class BulkOperationEventTester : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("Toggles whether or not the script should listen form debug commands.")]
        [SerializeField] private bool _isDebugActive = false;

        [Tooltip("The target grid that needs to be tested.")]
        [SerializeField] private InvGrid _gridToTest;

        [Tooltip("A list of grid operations. Used by the command 'RaiseBulkOperationEvent'.")]
        [SerializeField] private List<SerializableInvContentsUpdate> _paramSerializableOperationsList = new();
        private List<InvContentsUpdate> _operationsList = new();

        [Tooltip("Manually forces the grid to raise a bulkOperationEvent. These types of events must always be manually raised.")]
        [SerializeField] private bool _cmdRaiseBulkOperationEvent = false;



        private void Update()
        {
            if (_isDebugActive)
                ListenForDebugCommands();
        }


        private void ListenForDebugCommands()
        {
            if (_cmdRaiseBulkOperationEvent)
            {
                _cmdRaiseBulkOperationEvent = false;

                if (_gridToTest == null)
                {
                    Debug.LogWarning("Test grid is null. Can't raise bulkOperation event. Ignoring request.");
                }
                else
                {
                    _operationsList.Clear();

                    //convert the serializable version into the internal version
                    foreach (SerializableInvContentsUpdate serializableUpdate in _paramSerializableOperationsList)
                        _operationsList.Add(new InvContentsUpdate(serializableUpdate));

                    _gridToTest.ForceRaiseBulkInvContentsChanged(_operationsList);
                }
            }
        }


    }
}
