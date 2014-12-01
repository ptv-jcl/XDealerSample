using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Ptv.XServer.Controls.Map;
using Ptv.XServer.Controls.Map.Layers.Shapes;
using Ptv.XServer.Controls.Map.Symbols;
using XDealerSample.XRoute;

namespace XDealerSample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public readonly string srid = Ptv.Components.Projections.CoordinateReferenceSystem.XServer.PTV_MERCATOR.Id;
        ShapeLayer reachableObjectLayer;
        ShapeLayer isochroneLayer;

        public MainWindow()
        {
#if DEBUG
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
#endif

            InitializeComponent();
            mapControl.XMapUrl = XDealerSample.Properties.Settings.Default.XMapUrl;
            isochroneLayer = new ShapeLayer("Isochrone") { SpatialReferenceId = srid };
            mapControl.Layers.Add(isochroneLayer);
            reachableObjectLayer = new ShapeLayer("Reachable Objects") { SpatialReferenceId = srid };
            mapControl.Layers.Add(reachableObjectLayer);
        }

        private void mapControl_Loaded(object sender, RoutedEventArgs e)
        {
            DealerSearch(new System.Windows.Point(681583.7582082897, 6371865.264284901));
        }

        private void mapControl_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var geoPoint = mapControl.MouseToGeo(e, srid);
            DealerSearch(geoPoint);
        }

        private void DealerSearch(System.Windows.Point point)
        {
            Cursor = Cursors.Wait;
            mapControl.SetMapLocation(point, 12, srid);
            reachableObjectLayer.Shapes.Clear();
            isochroneLayer.Shapes.Clear();

            var waypoint = new WaypointDesc()
            {
                linkType = LinkType.NEXT_SEGMENT,
                wrappedCoords = new XRoute.Point[]
                { 
                    new XRoute.Point()
                    {
                        point = new PlainPoint()
                        {
                            x = point.X,
                            y= point.Y,
                        },
                    },
                },
            };

            var expansionDesc = new ExpansionDescription()
                {
                    expansionType = ExpansionType.EXP_TIME,
                    wrappedHorizons = new int[] { 900 },
                };

            var options = new ReachableObjectsOptions()
            {
                expansionDesc = expansionDesc,
                linkType = LinkType.NEXT_SEGMENT,
                routingDirection = RoutingDirectionType.FORWARD,
                geodatasourceLayer = XDealerSample.Properties.Settings.Default.GeoDataSource,
            };

            var cc = new CallerContext()
            {
                wrappedProperties = new CallerContextProperty[]
                {
                    new CallerContextProperty() {key="CoordFormat",value="PTV_MERCATOR"},
                    new CallerContextProperty() {key="Profile",value="carfast"},
                }
            };

            var isoOptions = new IsochroneOptions()
            {
                expansionDesc = expansionDesc,
                isoDetail = IsochroneDetail.POLYS_ONLY,
                polygonCalculationMode = PolygonCalculationMode.NODE_BASED,
            };

            ReachableObjects foundObjects = null;
            Isochrone isochrone = null;

            using (var xRouteClient = new XRouteWSClient())
            {
                try
                {
                    foundObjects = xRouteClient.searchForReachableObjects(waypoint, null, null, options, null, cc);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        "Exception while searching for objects.\n\nException type: " + exception.GetType().ToString() +
                        "\nMessage: " + exception.Message);
                    Cursor = null;
                    return;
                }
                try
                {
                    isochrone = xRouteClient.calculateIsochrones(waypoint, null, isoOptions, cc);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        "Exception while calculating isochrone.\n\nException type: " + exception.GetType().ToString() +
                        "\nMessage: " + exception.Message);
                    Cursor = null;
                    return;
                }
            }

            foreach (var foundObject in foundObjects.wrappedReachableObject)
            {
                var ball = new Ball()
                {
                    Height = 10,
                    Width = 10,
                    Tag = foundObject.@object.id,
                    ToolTip = "",
                    Color = Colors.Blue,
                };
                ball.ToolTipOpening += ball_ToolTipOpening;

                var winPoint = new System.Windows.Point()
                {
                    X = foundObject.@object.coord.point.x,
                    Y = foundObject.@object.coord.point.y,
                };
                ShapeCanvas.SetLocation(ball, winPoint);
                reachableObjectLayer.Shapes.Add(ball);
            }

            var linearRing = new NetTopologySuite.Geometries.LinearRing(
                isochrone.wrappedIsochrones[0].polys.lineString.wrappedPoints
                    .Select(p => new GeoAPI.Geometries.Coordinate(p.x, p.y))
                    .ToArray()
                    );
            linearRing.Normalize();

            var geom = new NetTopologySuite.Geometries.Polygon(linearRing);

            var bufferedGeom = geom.Buffer(100);
            var polygon = new MapPolygon()
            {
                Points = new PointCollection(bufferedGeom.Boundary.Coordinates.Select(c => new System.Windows.Point(c.X, c.Y))),
                Fill = new SolidColorBrush(Colors.AliceBlue),
                Opacity = 0.75,
                Stroke = new SolidColorBrush(Colors.DarkSlateGray)
            };
            isochroneLayer.Shapes.Add(polygon);

            Cursor = null;
        }

        void ball_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            var ball = ((Ball)sender);
            if (((string)ball.ToolTip) == "")
            {
                ball.ToolTip = "-- Insert custum code to retreive infromation from database for record with id " + ball.Tag.ToString() + " --";
            }
        }
    }
}
