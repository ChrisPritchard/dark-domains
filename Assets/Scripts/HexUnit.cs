
namespace HexMapTutorials
{
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;
    
    public class HexUnit : MonoBehaviour 
    {
        public HexGrid Grid { get; set; }

        public static HexUnit UnitPrefab;

        List<HexCell> pathToTravel;

        const float travelSpeed = 4f;
        const float rotationSpeed = 180f;

        HexCell location, currentTravelLocation;

        public int Speed => 24;

        public int VisionRange => 3;

        public HexCell Location
        {
            get => location;
            set
            {
                if (location == value)
                    return;
                if (location)
                {
                    Grid.DecreaseVisibility(location, VisionRange);
                    location.Unit = null;
                }
                location = value;
                value.Unit = this;
                Grid.IncreaseVisibility(value, VisionRange);
                transform.localPosition = value.Position;
                Grid.MakeChildOfColumn(transform, value.ColumnIndex);
            }
        }

        float orientation;
        public float Orientation
        {
            get => orientation;
            set
            {
                if (orientation == value)
                    return;
                orientation = value;
                transform.localRotation = Quaternion.Euler(0f, value, 0f);
            }
        }

        private void OnEnable() 
        {
            if(location) 
            {
                transform.localPosition = location.Position;
                if(currentTravelLocation) // catches recompile while moving... probably unnecessary
                {
                    Grid.IncreaseVisibility(location, VisionRange);
                    Grid.DecreaseVisibility(currentTravelLocation, VisionRange);
                    currentTravelLocation = null;
                }
            }
        }

        public void ValidatePosition() => transform.localPosition = location.Position;

        public void Die()
        {
            location.Unit = null;
            Grid.DecreaseVisibility(location, VisionRange);
            Destroy(gameObject);
        }

        public bool IsValidDestination(HexCell cell) => cell.IsExplored && !cell.IsUnderwater && !cell.Unit;

        public int GetMoveCost(HexCell fromCell, HexCell toCell, HexDirection direction)
        {
            var edgeType = fromCell.GetEdgeType(toCell);
            if (edgeType == HexEdgeType.Cliff)
                return -1;

            var moveCost = 10;
            if (fromCell.HasRoadThroughEdge(direction))
                moveCost = 1;
            else if(fromCell.Walled != toCell.Walled)
                return -1;
            else
            {
                if (edgeType == HexEdgeType.Flat)
                    moveCost = 5;
                moveCost += toCell.UrbanLevel + toCell.FarmLevel + toCell.ForestLevel;
            }

            return moveCost;
        }

        IEnumerator LookAt(Vector3 point)
        {
            if(HexMetrics.Wrapping)
            {
                var xDistance = point.x - transform.localPosition.x;
                if(xDistance < -HexMetrics.InnerRadius * HexMetrics.WrapSize)
                    point.x += HexMetrics.InnerDiameter * HexMetrics.WrapSize;
                else if(xDistance > HexMetrics.InnerRadius * HexMetrics.WrapSize)
                    point.x -= HexMetrics.InnerDiameter * HexMetrics.WrapSize;
            }

            point.y = transform.localPosition.y;
            var fromRotation = transform.localRotation;
            var toRotation = Quaternion.LookRotation(point - transform.localPosition);
            var angle = Quaternion.Angle(fromRotation, toRotation);

            if(angle > 0f)
            {
                var speed = rotationSpeed / angle;
                for(var t = Time.deltaTime * speed; t < 1f; t += Time.deltaTime * speed)
                {
                    transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
                    yield return null;
                }
            }

            transform.LookAt(point);
            orientation = transform.localRotation.eulerAngles.y;
        }

        public void Travel(List<HexCell> path)
        {
            location.Unit = null;
            location = path[path.Count - 1];
            location.Unit = this;

            pathToTravel = path;
            StopAllCoroutines();
            StartCoroutine(TravelPath());
        }

        private IEnumerator TravelPath()
        {
            Vector3 a, b, c = pathToTravel[0].Position;
            transform.localPosition = c;
            yield return LookAt(pathToTravel[1].Position);
            
            if(!currentTravelLocation)
                currentTravelLocation = pathToTravel[0];
                
            Grid.DecreaseVisibility(currentTravelLocation, VisionRange);
            var currentColumn = currentTravelLocation.ColumnIndex;

            var t = Time.deltaTime * travelSpeed;
            var wrapJump = HexMetrics.InnerDiameter * HexMetrics.WrapSize;

            for(var i = 1; i < pathToTravel.Count; i++)
            {
                currentTravelLocation = pathToTravel[i];
                a = c;
                b = pathToTravel[i - 1].Position;

                var nextColumn = currentTravelLocation.ColumnIndex;
                if(currentColumn != nextColumn)
                {
                    if(nextColumn < currentColumn - 1)
                    {
                        a.x -= wrapJump;
                        b.x -= wrapJump;
                    }
                    else if(nextColumn > currentColumn + 1)
                    {
                        a.x += wrapJump;
                        b.x += wrapJump;
                    }
                    Grid.MakeChildOfColumn(transform, nextColumn);
                    currentColumn = nextColumn;
                }

                c = (b + currentTravelLocation.Position) * 0.5f;
                Grid.IncreaseVisibility(pathToTravel[i], VisionRange);

                for(; t < 1f; t += Time.deltaTime * travelSpeed)
                {
                    transform.localPosition = Bezier.GetPoint(a, b, c, t);
                    var d = Bezier.GetDirivative(a, b, c, t);
                    d.y = 0f;
                    transform.localRotation = Quaternion.LookRotation(d);
                    yield return null;
                }
                
                Grid.DecreaseVisibility(pathToTravel[i], VisionRange);
                t -= 1f;
            }

            // final move towards center

            a = c;
            b = pathToTravel[pathToTravel.Count - 1].Position;
            c = b;
            
            Grid.IncreaseVisibility(Location, VisionRange);

            for(; t < 1f; t += Time.deltaTime * travelSpeed)
            {
                transform.localPosition = Bezier.GetPoint(a, b, c, t);
                var d = Bezier.GetDirivative(a, b, c, t);
                d.y = 0f;
                transform.localRotation = Quaternion.LookRotation(d);
                yield return null;
            }

            currentTravelLocation = null;
            transform.localPosition = Location.Position;
            Orientation = transform.localRotation.eulerAngles.y;
        }

        public void Save(BinaryWriter writer)
        {
            location.Coordinates.Save(writer);
            writer.Write(orientation);
        }

        public static void Load(BinaryReader reader, HexGrid grid)
        {
            var coords = HexCoordinates.Load(reader);
            var orientation = reader.ReadSingle();
            grid.AddUnit(Instantiate(UnitPrefab), grid.GetCell(coords), orientation);
        }
    }
}