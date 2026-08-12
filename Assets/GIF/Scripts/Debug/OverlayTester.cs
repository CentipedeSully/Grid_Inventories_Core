using GIF;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIF
{
    public class OverlayTester : MonoBehaviour
    {

        [Header("User Guidance")]
        [SerializeField]
        [TextArea(2, 6)]
        private string _overlayConsiderations = "It's recommended to create your own container layers if your adding overlays.\n\n" +
            "Simply create your container(s) as children of the 'GridArea' object, and then add the container(s) to the InvGrid's 'Other Containers' list (before runtime)";
        [SerializeField]
        [TextArea(6, 10)]
        private string _restrictedContainers = "If you must add to preexisting containers. The following are restricted for all grids:\n\n" +
            "'Grid': Contains Cells. DO NOT MODIFY\n" +
            "'unused Texts': Holds TMPro objects only. Don't modify\n\n" +
            "All other preexisting containers are safe to modify.";

        [Header("References")]
        [SerializeField] private InvGrid _targetGrid;

        [Header("Test Parameters")]
        [SerializeField] private RectTransform _overlay;
        [SerializeField] private Vector2Int _overlayPosition;
        [SerializeField] private RectTransform _targetGridContainer;
        [SerializeField] private bool _resizeOverlayToCellSize = false;


        private (int, int) _position;
        [Header("Commands")]
        [Tooltip("Enables the script to begin listening to commands. Keep disabled if you're not using it.")]
        [SerializeField] private bool _enableTesting = false;
        [SerializeField] private bool _cmdPerformOverlay = false;




        private void Update()
        {
            if (_enableTesting)
                ListenForCommands();
        }

        private void ListenForCommands()
        {
            if (_cmdPerformOverlay)
            {
                _cmdPerformOverlay = false;
                _position.Item1 = _overlayPosition.x;
                _position.Item2 = _overlayPosition.y;

                _targetGrid.OverlayRectTransformOntoGrid(_overlay, _targetGridContainer, _position, _resizeOverlayToCellSize);
            }
        }
    }
}
