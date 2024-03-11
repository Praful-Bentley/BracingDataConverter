//using BracingDataConverter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ReadAndParseBracingData
{
    internal class Program
    {
        static void Main(string[] args) {


            string text = File.ReadAllText("./../../../BracingDataConverter/input/towerStructure.json");
            var tsd = System.Text.Json.JsonSerializer.Deserialize<TowerStructureData>(text);
            towerDataStruct td = new towerDataStruct();
            td.base_altitude = tsd.base_altitude;
            td.base_center = tsd.base_center;
            td.base_width = tsd.base_width;
            td.bearing = tsd.bearing;
            td.concrete_height = tsd.concrete_height;
            td.epsg_code = tsd.epsg_code;
            td.lightning_rod_height = tsd.lightning_rod_height;
            td.nLegs = tsd.nLegs;
            td.top_altitude = tsd.top_altitude;
            td.top_appurtenance_height = tsd.top_appurtenance_height;
            td.top_steel_elevation = tsd.top_steel_elevation;
            td.top_width = tsd.top_width;
            td.tower_tilt_x = tsd.tower_tilt_x;
            td.tower_tilt_y = tsd.tower_tilt_y;
            td.tower_vertical_segments = tsd.tower_vertical_segments;
            //towerVerticalSegmentStruct[] tvs = new towerVerticalSegmentStruct()[];
            //tvs = tsd.tower_vertical_segments;
            //td.tower_vertical_segments = tsd.tower_vertical_segments as towerVerticalSegmentStruct;
            td.type = tsd.type;
            Console.WriteLine($"tsd : {td.tower_tilt_y}");
            //Console.WriteLine($"Last name: {tsd.base_center[0]}");
            //Console.WriteLine($"Job title: {tsd.base_width}");

            var abc = new ReadAndParseBracingJson("./../../../BracingDataConverter/input/bracings.json");
            var json = abc.useStreamFileOpenReadAll();
            List<bracingCoords> outputData = getBracingFaceDataForOTD(json, td);

            string json4 = JsonConvert.SerializeObject(outputData.ToArray());
            System.IO.File.WriteAllText(@"./../../../BracingDataConverter/output/FaceBracingsData.json", json4);
        }

        private static List<bracingCoords> getBracingFaceDataForOTD(List<BracingData> bay_pattern, towerDataStruct towerData, double height_offset = 0)
        {

            List<bracingCoords> topandBottomPoints = new List<bracingCoords>();

            if ((towerData.type != "lattice") && (towerData.type != "guyed"))
            {
                return topandBottomPoints;
            }
            for (var k = 0; k < bay_pattern.Count; k++)
            {
                // one bracing (from the data is one dict on the list of bracings)
                BracingData bay = bay_pattern[k];
                List<List<Double>> startLegCoord = getLegsCoordinatesAtAltitude(towerData, bay.start, height_offset);
                List<List<Double>> endLegCoord = getLegsCoordinatesAtAltitude(towerData, bay.end, height_offset);

                string PATTERN = bay.type;
                string HORIZONTAL_MIDDLE_BAY = ((bay.horizontalMid == false) ? "No" : "Yes");
                string HORIZONTAL_TOP_BAY = ((bay.horizontalTop == false) ? "No" : "Yes");


                // loop for each face
                for (var i = 0; i < towerData.nLegs; i++)
                {
                    // coords to send
                    //string[] coords = [""];

                    // 1st leg bottom and top coordinate
                    var l1_idx = i - 1;
                    if (l1_idx < 0) { l1_idx = towerData.nLegs - 1; }

                    List<double> l1_start = startLegCoord[l1_idx];
                    List<double> l1_end = endLegCoord[l1_idx];
                    // 2nd leg bottom and top coordinate
                    List<double> l2_start = startLegCoord[i];
                    List<double> l2_end = endLegCoord[i];
                    // coords.push([l1_start,l2_start])

                    bracingCoords bcCoord = new bracingCoords();

                    {
                        bcCoord.parent = bay.bracingId;
                        bcCoord.face = "Face " + i as string;
                        bcCoord.pattern = PATTERN;
                        bcCoord.hmbay = HORIZONTAL_MIDDLE_BAY;
                        bcCoord.htbay = HORIZONTAL_TOP_BAY;
                        bcCoord.bracingTopH = bay.end;
                        bcCoord.bracingBotH = bay.start;
                        bcCoord.node1 = l2_start;
                        bcCoord.node2 = l1_start;
                        bcCoord.node3 = l1_end;
                        bcCoord.node4 = l2_end;//l2_start
                    }
                    topandBottomPoints.Add(bcCoord);
                }
            }
            return topandBottomPoints;
        }

        public static List<List<Double>> getLegsCoordinatesAtAltitude(towerDataStruct towerData, double altitude, double height_offset = 0, double radius_offset = 0)
        {
            List<List<Double>> leg_coords = new List<List<double>>();
            double w, m, b;

            if ((towerData.type == "lattice") || (towerData.type == "guyed")) {
                // identify the vertical segment of analysis 
                int section_idx = -1;

                if (towerData.tower_vertical_segments.Count > 0) {
                    for (var j = 0; j < towerData.tower_vertical_segments.Count; j++) {
                        var section = towerData.tower_vertical_segments[j];
                        if ((section.altitude_bottom <= altitude) && (altitude < section.altitude_top)) {
                            section_idx = j;
                        }
                    }
                }
                // sraight line equation that define the tower width
                if (section_idx != -1) {
                    towerVerticalSegmentStruct section = towerData.tower_vertical_segments[section_idx];
                    double[,] numbers = { { section.width_bottom, section.altitude_bottom }, { section.top_width, section.altitude_top } };
                    double[] mb = StraightLineEq(numbers);
                    m = mb[0]; b = mb[1];
                }
                else {
                    double[,] numbers = { { towerData.base_width, towerData.base_altitude }, { towerData.top_width, towerData.top_altitude } };
                    double[] mb = StraightLineEq(numbers);
                    m = mb[0]; b = mb[1];
                }
                // width at a specific altitude
                w = (altitude - b) / m;
                ew em = new ew();
                em.elevation = altitude;
                em.width = w;

                //
                for (var i = 0; i < towerData.nLegs; i++) {
                    var leg_angle = (towerData.bearing * (Math.PI / 180) + (2 * Math.PI / towerData.nLegs) * i) % (2 * Math.PI);
                    // tower radius - distance from center to leg
                    var r = ((w / 2) / Math.Sin(Math.PI / towerData.nLegs)) + radius_offset;
                    // deviation on x/y axis due model tilt
                    var x_deviation = (altitude - towerData.base_altitude) * Math.Tan((towerData.tower_tilt_x * (Math.PI / 180)));
                    var y_deviation = (altitude - towerData.base_altitude) * Math.Tan((towerData.tower_tilt_y * (Math.PI / 180)));
                    double x = towerData.base_center[0] + r * Math.Sin(leg_angle) + x_deviation;
                    double y = towerData.base_center[1] + r * Math.Cos(leg_angle) + y_deviation;
                    double z = altitude + height_offset;
                    leg_coords.Add(new List<Double>() { x, y, z });

                }
            }
                return leg_coords;
        } 
        public static Double[] StraightLineEq(double[, ] line)
        {
            double x0, y0, x1, y1, m, b;
            x0 = line[0, 0];
            y0 = line[0, 1];
            x1 = line[1, 0];
            y1 = line[1, 1];
            // avoid div by 0
            if (x1 == x0)
            {
                m = 9999;
            }
            else
            {
                m = (y1 - y0) / (x1 - x0);
            }
            b = y0 - x0 * m;
            double[] ret = { m, b }; 
            return ret;
        }

        //private static BracingDataStruct[] GetBracingFaceDataForOTD(BracingData[] bay_pattern, towerDataStruct towerData, int height_offset = 0)
        //{


        //    // Create my object
        //    var myData = new
        //    {
        //        Host = @"sftp.myhost.gr",
        //        UserName = "my_username",
        //        Password = "my_password",
        //        SourceDir = "/export/zip/mypath/",
        //        FileName = "my_file.zip"
        //    };

        //    // Transform it to JSON object
        //    string jsonData = JsonConvert.SerializeObject(myData);

        //    // Print the JSON object
        //    Console.WriteLine(jsonData);

        //    // Parse the JSON object
        //    JObject jsonObject = JObject.Parse(jsonData);

        //    // Print the parsed JSON object
        //    Console.WriteLine((string)jsonObject["Host"]);
        //    Console.WriteLine((string)jsonObject["UserName"]);
        //    Console.WriteLine((string)jsonObject["Password"]);
        //    Console.WriteLine((string)jsonObject["SourceDir"]);
        //    Console.WriteLine((string)jsonObject["FileName"]);


        //    //// Create my object
        //    //var myData = new
        //    //{
        //    //    Host = @"sftp.myhost.gr",
        //    //    UserName = "my_username",
        //    //    Password = "my_password",
        //    //    SourceDir = "/export/zip/mypath/",
        //    //    FileName = "my_file.zip"
        //    //};

        //    //// Transform it to JSON object
        //    //string jsonData = JsonConvert.SerializeObject(myData);

        //    //// Print the JSON object
        //    //Console.WriteLine(jsonData);

        //    //// Parse the JSON object
        //    //JObject jsonObject = JObject.Parse(jsonData);

        //    //// Print the parsed JSON object
        //    //Console.WriteLine((string)jsonObject["Host"]);
        //    //Console.WriteLine((string)jsonObject["UserName"]);
        //    //Console.WriteLine((string)jsonObject["Password"]);
        //    //Console.WriteLine((string)jsonObject["SourceDir"]);
        //    //Console.WriteLine((string)jsonObject["FileName"]);

        //    BracingDataStruct[] topandBottomPoints = { };

        //    if ((towerData.type != "lattice") && (towerData.type != "guyed"))
        //    {
        //        return topandBottomPoints;
        //    }
        //    for (var k = 0; k < bay_pattern.Length; k++)
        //    {
        //        // one bracing (from the data is one dict on the list of bracings)
        //        BracingData bay = bay_pattern[k];
        //        double[] startLegCoord = this.getLegsCoordinatesAtAltitude(towerData, bay.start, height_offset);
        //        double[] endLegCoord = this.getLegsCoordinatesAtAltitude(towerData, bay.end, height_offset);

        //        string PATTERN = bay.type;
        //        string HORIZONTAL_MIDDLE_BAY = ((bay.horizontalMid == false) ? "No" : "Yes");
        //        string HORIZONTAL_TOP_BAY = ((bay.horizontalTop == false) ? "No" : "Yes");

        //        //var mid = this.getMidBay(towerData, bay['start'], bay['end']);
        //        //var centerLegCoord = await this.getLegsCoordinatesAtAltitude(towerData, mid, height_offset = height_offset);

        //        // loop for each face
        //        for (var i = 0; i < towerData.nLegs; i++)
        //        {
        //            // coords to send
        //            //string[] coords = [""];

        //            // 1st leg bottom and top coordinate
        //            var l1_idx = i - 1;
        //            if (l1_idx < 0){l1_idx = towerData.nLegs - 1;}

        //            double l1_start = startLegCoord[l1_idx];
        //            double l1_end = endLegCoord[l1_idx];
        //            // 2nd leg bottom and top coordinate
        //            double l2_start = startLegCoord[i];
        //            double l2_end = endLegCoord[i];
        //            // coords.push([l1_start,l2_start])

        //            bracingCoords bcCoord = new bracingCoords();

        //            {
        //                bcCoord.parent = bay.bracingId;
        //                bcCoord.face = "Face " + i as string;
        //                bcCoord.pattern = PATTERN;
        //                bcCoord.hmbay = HORIZONTAL_MIDDLE_BAY;
        //                bcCoord.htbay = HORIZONTAL_TOP_BAY;
        //                bcCoord.bracingTopH = bay.end;
        //                bcCoord.bracingBotH = bay.start;
        //                bcCoord.node1 = l2_start;
        //                bcCoord.node2 = l1_start;
        //                bcCoord.node3 = l1_end;
        //                bcCoord.node4 = l2_end;//l2_start
        //            }
        //            //Aditya new----------
        //            topandBottomPoints.SetValue(bcCoord, topandBottomPoints.Length);
        //    //Work on propery storing each top and bottom horizontal points to rotate
        //    // BracingDecorator.topandBottomPoints.push({ pattern: PATTERN, hmbay: HORIZONTAL_MIDDLE_BAY, htbay: HORIZONTAL_TOP_BAY, l2_end: l2_end, l1_end: l1_end, l1_start: l1_start, l2_start: l2_start });
        //        }
        //    }
        //    return topandBottomPoints;
        //}



        public class ReadAndParseBracingJson
        {
            private readonly string _bracingJsonFilePath;
            private readonly JsonSerializerOptions _option = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            //public class ReadBracingData
            //{

            public ReadAndParseBracingJson(string bracingJsonFilePath)
            {
                _bracingJsonFilePath = bracingJsonFilePath;
            }
            //}

            public List<BracingData> useFileReadAll()
            {
                var json = File.ReadAllText(_bracingJsonFilePath);
                var bracingData = System.Text.Json.JsonSerializer.Deserialize<List<BracingData>>(json, _option);

                return bracingData;
            }
            public List<BracingData> useStreamFileOpenReadAll()
            {
                using StreamReader streamReader = new StreamReader(_bracingJsonFilePath);
                var json = streamReader.ReadToEnd();
                var bracingData = System.Text.Json.JsonSerializer.Deserialize<List<BracingData>>(json, _option);

                return bracingData;
            }
        }


        public class PlanBracingTop
        {
            public string type { get; set; }
            public double cert { get; set; }
        }

        public class BracingData
        {
            public int bracingId { get; set; }
            public double cert { get; set; }
            public double end { get; set; }
            public bool horizontalMid { get; set; }
            public bool horizontalTop { get; set; }
            public double start { get; set; }
            public string type { get; set; }
            public object planBracingMid { get; set; }
            public PlanBracingTop planBracingTop { get; set; }
        }



        public class TowerStructureData
        {
            public double base_altitude { get; set; }
            public List<double> base_center { get; set; }
            public double base_width { get; set; }
            public double bearing { get; set; }
            public double concrete_height { get; set; }
            public int epsg_code { get; set; }
            public object lightning_rod_height { get; set; }
            public int nLegs { get; set; }
            public double top_altitude { get; set; }
            public double top_appurtenance_height { get; set; }
            public double top_steel_elevation { get; set; }
            public double top_width { get; set; }
            public double tower_tilt_x { get; set; }
            public double tower_tilt_y { get; set; }
            public List<towerVerticalSegmentStruct> tower_vertical_segments { get; set; }
            public string type { get; set; }
        }

        public class TowerVerticalSegment
        {
            public double altitude_bottom { get; set; }
            public double altitude_top { get; set; }
            public double height { get; set; }
            public double width_bottom { get; set; }
            public double top_width { get; set; }
        }

    }
    struct towerDataStruct
    {
        public double base_altitude { get; set; }
        public List<double> base_center { get; set; }
        public double base_width { get; set; }
        public double bearing { get; set; }
        public double concrete_height { get; set; }
        public int epsg_code { get; set; }
        public object lightning_rod_height { get; set; }
        public int nLegs { get; set; }
        public double top_altitude { get; set; }
        public double top_appurtenance_height { get; set; }
        public double top_steel_elevation { get; set; }
        public double top_width { get; set; }
        public double tower_tilt_x { get; set; }
        public double tower_tilt_y { get; set; }
        public List<towerVerticalSegmentStruct> tower_vertical_segments { get; set; }
        public string type { get; set; }

    }

    struct towerVerticalSegmentStruct
    {
        public double altitude_bottom { get; set; }
        public double altitude_top { get; set; }
        public double height { get; set; }
        public double width_bottom { get; set; }
        public double top_width { get; set; }
    }

    struct bracingCoords
    {
        public int parent;
        public string face;
        public string pattern;
        public string hmbay;
        public string htbay;
        public double bracingTopH;
        public double bracingBotH;
        public List<double> node1;
        public List<double> node2;
        public List<double> node3;
        public List<double> node4;

    }

    struct BracingDataStruct
    {
        public int bracingId { get; set; }
        public double cert { get; set; }
        public double end { get; set; }
        public bool horizontalMid { get; set; }
        public bool horizontalTop { get; set; }
        public double start { get; set; }
        public string type { get; set; }
        public object planBracingMid { get; set; }
        public object planBracingTop { get; set; }
    }
    struct ew { 
        public double elevation;
        public double width; 
    }


}
