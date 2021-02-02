
namespace DarkDomains
{
    using System.IO;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    
    public enum BrushMode { Terrain, Elevation, WaterLevel, Rivers, Roads, Features, Walls, SpecialFeatures }

    public class HexGridEditor : MonoBehaviour 
    {
        const int version = 0;

        public HexGrid HexGrid;

        public BrushMode Mode;
        public GameObject[] BrushOptions;

        byte activeTerrain, activeSpecialFeature;

        byte activeElevation;
        public Text ElevationText;

        byte activeWaterLevel;
        public Text WaterLevelText;

        bool addRivers = true;

        bool addRoads = true;

        bool addWalls = true;

        byte activeUrbanLevel, activeFarmLevel, activeForestLevel;
        public Text UrbanLevelText, FarmLevelText, ForestLevelText;
        
        int brushSize = 1;
        public Text BrushSizeText;

        new Camera camera;
        EventSystem eventSystem;

        bool isDrag;
        HexDirection dragDirection;
        HexCell previousCell;
        HexCell prevPreviousCell;

        private void Awake() 
        {
            camera = Camera.main;
            eventSystem = EventSystem.current;
            SelectBrushMode(0); // start with terrain options shown
        }

        private void Update() 
        {
            if(Input.GetMouseButton(0) && !eventSystem.IsPointerOverGameObject())
                HandleInput();
            else
                previousCell = null;
        }

        private void HandleInput()
        {
            var inputRay = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(inputRay, out RaycastHit hit))
            {
                var target = HexGrid.GetCell(hit.point);
                EditCells(target);
                prevPreviousCell = previousCell;
                previousCell = target;
            }
            else
                previousCell = null;
        }

        private void EditCells(HexCell center)
        {
            // prevPrevious cell prevents quick double backs - i *think* this works
            if(previousCell != null && previousCell != center && prevPreviousCell != null && prevPreviousCell != center)
                isDrag = ValidateDrag(center);
            else
                isDrag = false;

            var c = center.Coordinates;
            var b = brushSize - 1; // when converted to radius, ignore centre

            for (int r = 0, z = c.Z - b; z <= c.Z; z++, r++)
                for (var x = c.X - r; x <= c.X + b; x++)
                    EditCell(HexGrid.GetCell(new HexCoordinates(x, z)));
            for (int r = 0, z = c.Z + b; z > c.Z; z--, r++)
                for (var x = c.X - b; x <= c.X + r; x++)
                    EditCell(HexGrid.GetCell(new HexCoordinates(x, z)));
        }

        private void EditCell(HexCell cell)
        {
            if(!cell)
                return;

            if (Mode == BrushMode.Terrain)
                cell.TerrainTypeIndex = activeTerrain;
            if(Mode == BrushMode.Elevation)
                cell.Elevation = activeElevation;
            if(Mode == BrushMode.WaterLevel)
                cell.WaterLevel = activeWaterLevel;
            if(Mode == BrushMode.Rivers && !addRivers)
                cell.RemoveRiver();
            if(Mode == BrushMode.Rivers && addRivers && isDrag)
                previousCell.SetOutgoingRiver(dragDirection);
            if(Mode == BrushMode.Roads && !addRoads)
                cell.RemoveRoad();
            if(Mode == BrushMode.Roads && addRoads && isDrag)
                previousCell.AddRoad(dragDirection); 
            if(Mode == BrushMode.Features)
            {
                cell.UrbanLevel = activeUrbanLevel;
                cell.FarmLevel = activeFarmLevel;
                cell.ForestLevel = activeForestLevel;
            }
            if(Mode == BrushMode.Walls)
                cell.Walled = addWalls;
            if(Mode == BrushMode.SpecialFeatures)
                cell.SpecialFeatureIndex = activeSpecialFeature;
        }

        // test if cell is neighbour of previous cell
        private bool ValidateDrag(HexCell newCell)
        {
            for(dragDirection = HexDirection.NE; dragDirection <= HexDirection.NW; dragDirection++)
                if(previousCell.GetNeighbour(dragDirection) == newCell)
                    return true;
            return false;
        }

        public void SelectBrushMode(int mode)
        { 
            for(var i = 0; i < BrushOptions.Length; i++)
                BrushOptions[i].SetActive(i == mode);
            Mode = (BrushMode)mode;
            Debug.Log("Mode is now " + Mode);
        }

        public void SelectTerrain(int index) => activeTerrain = (byte)index;

        public void SelectElevation(float amount)
        {
            activeElevation = (byte)amount;
            ElevationText.text = amount.ToString();
        }

        public void SelectWaterLevel(float amount)
        {
            activeWaterLevel = (byte)amount;
            WaterLevelText.text = amount.ToString();
        }

        public void SelectUrbanFeatureLevel(float amount)
        {
            activeUrbanLevel = (byte)amount;
            UrbanLevelText.text = amount.ToString();
        }

        public void SelectFarmFeatureLevel(float amount)
        {
            activeFarmLevel = (byte)amount;
            FarmLevelText.text = amount.ToString();
        }

        public void SelectForestFeatureLevel(float amount)
        {
            activeForestLevel = (byte)amount;
            ForestLevelText.text = amount.ToString();
        }

        public void SelectBrushSize(float amount)
        {
            brushSize = (int)amount;
            BrushSizeText.text = brushSize.ToString();
        }

        public void AddRivers(bool value) => addRivers = value;

        public void AddRoads(bool value) => addRoads = value;

        public void AddWalls(bool value) => addWalls = value;

        public void SelectSpecialFeature(int index) => activeSpecialFeature = (byte)index;

        public void ShowUI(bool visible) => HexGrid.ShowUI(visible);

        private string SavePath() => Path.Combine(Application.persistentDataPath, "test.map");

        public void Save()
        {
            using (var file = File.Open(SavePath(), FileMode.Create))
            using (var writer = new BinaryWriter(file))
            { 
                writer.Write(version);
                HexGrid.Save(writer); 
            }
            Debug.Log("Saved to " + SavePath());
        }

        public void Load()
        {
            using (var file = File.OpenRead(SavePath()))
            using (var reader = new BinaryReader(file))
            { 
                var fileVersion = reader.ReadInt32();
                if(fileVersion != version)
                    Debug.Log("invalid version in save file: " + fileVersion);
                else
                {
                    HexGrid.Load(reader); 
                    Debug.Log("Loaded from " + SavePath());
                }
            }
        }
    }
}