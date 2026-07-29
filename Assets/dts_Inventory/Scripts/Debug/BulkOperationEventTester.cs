using dtsInventory;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace dtsInventory
{
    public class BulkOperationEventTester : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("Toggles whether or not the script should listen form debug commands.")]
        [SerializeField] private bool _isDebugActive = false;

        [Tooltip("The target grid that needs to be tested.")]
        [SerializeField] private InvGrid _gridToTest;

        [Tooltip("A list of grid operations. Used by the command 'RaiseBulkOperationEvent'.")]
        [SerializeField] private List<InvContentsUpdate> _paramOperationsList = new();

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
                    _gridToTest.ForceRaiseBulkInvContentsChanged(_paramOperationsList);
                }
            }
        }


    }
}
