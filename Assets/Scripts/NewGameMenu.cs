
namespace HexMapTutorials
{
    using UnityEngine;
    
    public class NewGameMenu : MonoBehaviour 
    {
        public HexGrid HexGrid;
        public HexMapCamera HexMapCamera;
        public HexMapGenerator Generator;

        public void Show()
        {
            HexMapCamera.Locked = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            HexMapCamera.Locked = false;
        }

        private bool generateMap = true;

        public void ToggleMapGeneration (bool toggle) => generateMap = toggle;

        private bool wrappingMap = true;

        public void ToggleWrappingMap (bool toggle) => wrappingMap = toggle;

        public void CreateSmallMap()
        {
            if(generateMap)
                Generator.GenerateMap(20, 15, wrappingMap);
            else
                HexGrid.CreateMap(20, 15, wrappingMap);
            Hide();
            HexMapCamera.ValidatePosition();
        }

        public void CreateMediumMap()
        {
            if(generateMap)
                Generator.GenerateMap(40, 30, wrappingMap);
            else
                HexGrid.CreateMap(40, 30, wrappingMap);
            Hide();
            HexMapCamera.ValidatePosition();
        }

        public void CreateLargeMap()
        {
            if(generateMap)
                Generator.GenerateMap(80, 60, wrappingMap);
            else
                HexGrid.CreateMap(80, 60, wrappingMap);
            Hide();
            HexMapCamera.ValidatePosition();
        }
    }
}