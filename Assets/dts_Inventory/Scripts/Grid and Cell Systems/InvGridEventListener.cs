using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace dtsInventory
{
    /// <summary>
    /// This class listens to an InvGrid's internal events and triggers the corresponding UnityEvents in response to the original Grid's internal events.
    /// </summary>
    public class InvGridEventListener : MonoBehaviour
    {
        private InvGrid _siblingGrid;

        [Header("Unity Events")]
        [Tooltip("Signals when an 'addItem' operation succeeds in the sibling InvGrid component. Provides both the itemData and the amount added to the grid.")]
        public UnityEvent<ItemData, int> OnItemAdded;
        [Tooltip("Signals when a 'removeItem' operation succeeds in the sibling InvGrid component. Provides both the itemData and the amount removed from the grid.")]
        public UnityEvent<ItemData, int> OnItemRemoved;
        [Tooltip("Signals when the batched operation event is performed in the sibling InvGrid component. Provides a list of all the operations that occured in the grid.")]
        public UnityEvent<List<InvContentsUpdate>> OnBulkOperationOccured;
        [Tooltip("Signals when 'Destroy' is called on the sibling grid. Provides a reference to the grid before its destroyed (in case you need to update other scripts of its demise).")]
        public UnityEvent<InvGrid> OnGridDestroyImminent;
        [Tooltip("Signals when the sibling InvGrid has been resized. Provides the grid's new dimensions.")]
        public UnityEvent<Vector2> OnGridResized;

        [Header("Debug")]
        [SerializeField] private bool _logSubscriptionStatus = false;
        [SerializeField] private bool _logEvents = false;



        //monobehaviours

        private void OnEnable()
        {
            DetectAndSubscribeToSiblingGrid();
        }
        private void OnDisable()
        {
            UnsubAndClearSiblingGrid();
        }
        private void OnDestroy()
        {
            UnsubAndClearSiblingGrid();
        }




        //internals
        private void DetectAndSubscribeToSiblingGrid()
        {
            _siblingGrid = GetComponent<InvGrid>();
            if (_siblingGrid != null)
            {
                _siblingGrid.OnContentsChanged += RespondToGridOperation;
                _siblingGrid.OnBulkContentsChanged += RespondToBulkOperation;
                _siblingGrid.OnGridDestroyed += RespondToGridDestruction;
                _siblingGrid.OnGridResized += RespondToGridResize;

                if (_logSubscriptionStatus)
                {
                    Debug.Log($"Subscribed to InvGrid '{_siblingGrid.name}'");
                }
            }
        }
        private void UnsubAndClearSiblingGrid()
        {
            if (_siblingGrid != null)
            {
                _siblingGrid.OnContentsChanged -= RespondToGridOperation;
                _siblingGrid.OnBulkContentsChanged -= RespondToBulkOperation;
                _siblingGrid.OnGridDestroyed -= RespondToGridDestruction;
                _siblingGrid.OnGridResized -= RespondToGridResize;

                if (_logSubscriptionStatus)
                {
                    Debug.Log($"Unsubscribed from InvGrid '{_siblingGrid.name}'");
                }

                _siblingGrid = null;
            }
            
        }
        private void RespondToGridOperation(InvContentsUpdate update)
        {
            switch (update.operation)
            {
                case InvOperation.Add:
                    if (_logEvents)
                    {
                        Debug.Log($"Added {update.amount} {update.itemData}(s) to InvGrid '{_siblingGrid.name}'");
                    }

                    OnItemAdded?.Invoke(update.itemData, update.amount);
                    break;

                case InvOperation.Remove:
                    if (_logEvents)
                    {
                        Debug.Log($"Removed {update.amount} {update.itemData}(s) from InvGrid '{_siblingGrid.name}'");
                    }

                    OnItemRemoved?.Invoke(update.itemData, update.amount);
                    break;

                default:
                    break;
            }
        }
        private void RespondToBulkOperation(List<InvContentsUpdate> operationList)
        {
            List<InvContentsUpdate> returnList = new List<InvContentsUpdate>();
            string debugString = "Batch Operation Detected:\n";


            foreach (InvContentsUpdate operation in operationList)
            {
                returnList.Add(operation);

                if (_logEvents)
                {
                    debugString += $"Operation: [{operation.operation}] , ItemSubject: [{operation.itemData.Name()}], Amount: [{operation.amount}]\n";
                }
            }

            if (_logEvents)
                Debug.Log(debugString);

            OnBulkOperationOccured?.Invoke(returnList);
        }
        private void RespondToGridDestruction()
        {

            if (_logEvents)
            {
                Debug.Log($"Detected imminent destruction of InvGrid '{_siblingGrid.name}'");
            }
            OnGridDestroyImminent?.Invoke(_siblingGrid);
            UnsubAndClearSiblingGrid();
        }
        private void RespondToGridResize(Vector2 newDimensions)
        {
            if (_logEvents)
                Debug.Log($"InvGrid '{_siblingGrid.name}' resized to new dimensions: ({newDimensions.x},{newDimensions.y})");

            OnGridResized?.Invoke(newDimensions);
        }





    }
}

