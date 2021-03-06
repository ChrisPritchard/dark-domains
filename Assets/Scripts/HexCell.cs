
namespace HexMapTutorials
{
    using System.IO;
    using UnityEngine;
    using UnityEngine.UI;

    public class HexCell : MonoBehaviour
    {
        public int Index { get; set; }
        public int ColumnIndex { get; set; }
        public HexGridChunk Chunk;
        public HexCoordinates Coordinates;
        public RectTransform UIRect;

        public HexCellShaderData ShaderData { get; set; }
        public HexUnit Unit { get; set; }

        private bool hasIncomingRiver, hasOutgoingRiver;
        private HexDirection incomingRiver, outgoingRiver;

        [SerializeField]
        private bool[] roads;

        private bool walled;

        public Vector3 Position => transform.localPosition;

        // search params
        public int Distance { get; set; }
        public HexCell PathFrom { get; set; }
        public int SearchHeuristic { get; set; }
        public int SearchPriority => Distance + SearchHeuristic;
        public HexCell NextWithSamePriority { get; set; }
        public int SearchPhase { get; set; }

        private int visibility;
        private bool explored;

        public bool IsVisible => Explorable && visibility > 0;
        public bool IsExplored { get => Explorable && explored; private set => explored = value; }
        public bool Explorable { get; set; }

        private int terrainTypeIndex;
        public int TerrainTypeIndex
        {
            get => terrainTypeIndex;
            set
            {
                if (terrainTypeIndex == value)
                    return;
                terrainTypeIndex = value;
                ShaderData.RefreshTerrain(this);
            }
        }

        private int elevation;
        public int Elevation
        {
            get => elevation;
            set
            {
                if(value == elevation)
                    return;
                var viewElevation = ViewElevation;
                elevation = value;
                if(viewElevation != ViewElevation)
                    ShaderData.ViewElevationChanged();
                RefreshPosition();
                ValidateRivers();
                for(var direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
                    if(HasRoadThroughEdge(direction) && GetElevationDifference(direction) > HexMetrics.MaxRoadSlope)
                        SetRoad((int)direction, false);
                Refresh();
            }
        }

        private int waterLevel;
        public int WaterLevel
        {
            get => waterLevel;
            set
            {
                if (waterLevel == value)
                    return;
                var viewElevation = ViewElevation;
                waterLevel = value;
                if(viewElevation != ViewElevation)
                    ShaderData.ViewElevationChanged();
                ValidateRivers();
                Refresh();
            }
        }

        public bool IsUnderwater => waterLevel > elevation;

        public int ViewElevation => elevation >= waterLevel ? elevation : waterLevel;

        byte urbanLevel, farmLevel, forestLevel;

        public byte UrbanLevel
        {
            get => urbanLevel;
            set
            {
                if (urbanLevel == value)
                    return;
                urbanLevel = value;
                Refresh();
            }
        }

        public byte FarmLevel
        {
            get => farmLevel;
            set
            {
                if (farmLevel == value)
                    return;
                farmLevel = value;
                Refresh();
            }
        }

        public byte ForestLevel
        {
            get => forestLevel;
            set
            {
                if (forestLevel == value)
                    return;
                forestLevel = value;
                Refresh();
            }
        }
        public bool Walled
        {
            get => walled;
            set
            {
                if (walled == value)
                    return;
                walled = value;
                Refresh();
            }
        }

        byte specialFeatureIndex;
        public byte SpecialFeatureIndex
        {
            get => specialFeatureIndex;
            set
            {
                if (specialFeatureIndex == value || HasRiver)
                    return;
                specialFeatureIndex = value;
                RefreshSelfOnly();
            }
        }

        public bool IsSpecial => SpecialFeatureIndex > 0;

        public float StreamBedY => (elevation + HexMetrics.StreamBedElevationOffset) * HexMetrics.ElevationStep;

        public float RiverSurfaceY => (elevation + HexMetrics.WaterElevationOffset) * HexMetrics.ElevationStep;

        public float WaterSurfaceY => (waterLevel + HexMetrics.WaterElevationOffset) * HexMetrics.ElevationStep;

        public bool HasIncomingRiver => hasIncomingRiver;
        public bool HasOutgoingRiver => hasOutgoingRiver;
        public HexDirection IncomingRiver => incomingRiver;
        public HexDirection OutgoingRiver => outgoingRiver;
        public bool HasRiver => hasIncomingRiver || hasOutgoingRiver;
        public bool HasRiverBeginOrEnd => hasIncomingRiver != hasOutgoingRiver;
        public HexDirection RiverBeginOrEndDirection => hasIncomingRiver ? incomingRiver : outgoingRiver;

        public bool HasRoads
        {
            get
            {
                if (IsUnderwater)
                    return false;
                foreach(var road in roads)
                    if (road) return true;
                return false;
            }
        }

        public bool HasRoadThroughEdge(HexDirection direction)
        {
            if (IsUnderwater || (GetNeighbour(direction) != null && GetNeighbour(direction).IsUnderwater))
                return false;
            return roads[(int)direction];
        }

        public void RemoveRoad()
        {
            for(var i = 0; i < Neighbours.Length; i++) 
            {
                if(!roads[i])
                    continue;
                SetRoad(i, false);
            }
        }

        public void AddRoad(HexDirection direction)
        {
            if(!roads[(int)direction] && !HasRiverThroughEdge(direction) 
                && GetElevationDifference(direction) <= HexMetrics.MaxRoadSlope)
                SetRoad((int)direction, true);
        }

        private void SetRoad(int index, bool state)
        {
            var neighbour = Neighbours[index];
            if(!neighbour) 
                return;
            roads[index] = state;
            neighbour.roads[(int)((HexDirection)index).Opposite()] = state;
            neighbour.RefreshSelfOnly();
            RefreshSelfOnly();
        }

        public int GetElevationDifference(HexDirection direction) => Mathf.Abs(elevation - GetNeighbour(direction).elevation);

        [SerializeField]
        public HexCell[] Neighbours;

        public HexCell GetNeighbour(HexDirection direction) => Neighbours[(int)direction];

        public void SetNeighbour(HexDirection direction, HexCell cell)
        {
            Neighbours[(int)direction] = cell;
            cell.Neighbours[(int)direction.Opposite()] = this;
        }

        public HexEdgeType GetEdgeType(HexCell other) => HexMetrics.GetEdgeType(elevation, other.elevation);

        public void IncreaseVisibility()
        {
            visibility++;
            if(visibility == 1)
            {
                IsExplored = true;
                ShaderData.RefreshVisibility(this);
            }
        }

        public void DecreaseVisibility()
        {
            visibility--;
            if(visibility == 0)
                ShaderData.RefreshVisibility(this);
        }

        public void ResetVisibility()
        {
            if(visibility > 0)
            {
                visibility = 0;
                ShaderData.RefreshVisibility(this);
            }
        }

        public bool HasRiverThroughEdge(HexDirection direction) =>
            (hasIncomingRiver && incomingRiver == direction) || (hasOutgoingRiver && outgoingRiver == direction);

        public void RemoveOutgoingRiver()
        {
            if (!hasOutgoingRiver)
                return;
            hasOutgoingRiver = false;
            RefreshSelfOnly();

            var neighbour = GetNeighbour(outgoingRiver);
            neighbour.hasIncomingRiver = false;
            neighbour.RefreshSelfOnly();
        }

        public void RemoveIncomingRiver()
        {
            if (!hasIncomingRiver)
                return;
            hasIncomingRiver = false;
            RefreshSelfOnly();

            var neighbour = GetNeighbour(incomingRiver);
            neighbour.hasOutgoingRiver = false;
            neighbour.RefreshSelfOnly();
        }

        public void RemoveRiver()
        {
            RemoveOutgoingRiver();
            RemoveIncomingRiver();
        }

        public void SetOutgoingRiver(HexDirection direction)
        {
            if (hasOutgoingRiver && outgoingRiver == direction)
                return;

            var neighbour = GetNeighbour(direction);
            if(!IsValidRiverDestination(neighbour))
                return;

            RemoveOutgoingRiver(); // clear existing, if it exists
            if (hasIncomingRiver && incomingRiver == direction)
                RemoveIncomingRiver();
            
            outgoingRiver = direction;
            hasOutgoingRiver = true;
            specialFeatureIndex = 0;

            neighbour.RemoveIncomingRiver();
            neighbour.hasIncomingRiver = true;
            neighbour.incomingRiver = direction.Opposite();
            neighbour.specialFeatureIndex = 0;

            SetRoad((int)direction, false); // this will also refresh this cell
        }

        private bool IsValidRiverDestination(HexCell neighbour) =>
            neighbour && (elevation >= neighbour.elevation || waterLevel == neighbour.elevation);

        private void ValidateRivers()
        {
            if (hasOutgoingRiver && !IsValidRiverDestination(GetNeighbour(outgoingRiver)))
                RemoveOutgoingRiver();
            if (hasIncomingRiver && !GetNeighbour(incomingRiver).IsValidRiverDestination(this))
                RemoveIncomingRiver();
        }

        private void RefreshPosition()
        {
             var position = transform.localPosition;
            position.y = elevation * HexMetrics.ElevationStep;
            position.y += (HexMetrics.SampleNoise(position).y * 2f - 1f) * HexMetrics.ElevationPerturbStrength;
            transform.localPosition = position;

            var uiPosition = UIRect.localPosition;
            uiPosition.z = -position.y;
            UIRect.localPosition = uiPosition;
        }

        public void SetLabel(string text) => UIRect.GetComponent<Text>().text = text;

        public void DisableHighlight() => UIRect.GetChild(0).GetComponent<Image>().enabled = false;

        public void EnableHighlight(Color colour)
        {
            var highlight = UIRect.GetChild(0).GetComponent<Image>();
            highlight.color = colour;
            highlight.enabled = true;
        }

        public void SetMapData(float data) => ShaderData.SetMapData(this, data);

        public void Refresh()
        {
            if(!Chunk) 
                return;

            Chunk.Refresh();
            foreach(var neighbour in Neighbours)
                if(neighbour != null && neighbour.Chunk != Chunk)
                    neighbour.Chunk.Refresh();

            if(Unit)
                Unit.ValidatePosition();
        }

        public void RefreshSelfOnly()
        {
            Chunk?.Refresh();
            if(Unit)
                Unit.ValidatePosition();
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write((byte)(elevation + 127));
            writer.Write((byte)(waterLevel + 127));
            writer.Write((byte)terrainTypeIndex);

            var roadFlags = 0;
            for(var i = 0; i < roads.Length; i++)
                if(roads[i]) roadFlags |= 1 << i;
            writer.Write((byte)roadFlags);

            writer.Write((byte)(hasIncomingRiver ? incomingRiver + 128 : 0));
            writer.Write((byte)(hasOutgoingRiver ? outgoingRiver + 128 : 0));
            writer.Write(urbanLevel);
            writer.Write(farmLevel);
            writer.Write(forestLevel);
            writer.Write(specialFeatureIndex);
            writer.Write(walled);
            writer.Write(IsExplored);
        }

        public void Load(BinaryReader reader)
        {
            elevation = (int)(reader.ReadByte() - 127);
            waterLevel = (int)(reader.ReadByte() - 127);
            terrainTypeIndex = reader.ReadByte();

            ShaderData.RefreshTerrain(this);

            var roadFlags = reader.ReadByte();
            for(var i = 0; i < roads.Length; i++)
                roads[i] = (roadFlags & (1 << i)) != 0;

            var incoming = reader.ReadByte();
            hasIncomingRiver = incoming >= 128;
            if(hasIncomingRiver)
                incomingRiver = (HexDirection)(incoming - 128);

            var outgoing = reader.ReadByte();
            hasOutgoingRiver = outgoing >= 128;
            if(hasOutgoingRiver)
                outgoingRiver = (HexDirection)(outgoing - 128);
                
            urbanLevel = reader.ReadByte();
            farmLevel = reader.ReadByte();
            forestLevel = reader.ReadByte();
            specialFeatureIndex = reader.ReadByte();
            walled = reader.ReadBoolean();

            RefreshPosition();

            IsExplored = reader.ReadBoolean();
            ShaderData.RefreshVisibility(this);
        }
    }
}