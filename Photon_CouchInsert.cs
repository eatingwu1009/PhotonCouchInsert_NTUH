using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using CouchInsert;
using System.IO;
using System.ComponentModel;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]
namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext scriptContext, ScriptEnvironment environment)
        {
            // TODO : Add here the code that is called when the script is launched from Eclipse.
            VVector SIU = scriptContext.Image.UserOrigin;
            Image SI = scriptContext.Image;
            StructureSet SS = scriptContext.StructureSet;
            double chkOrientation = new double();
            PatientOrientation orientation = scriptContext.Image.ImagingOrientation;
            if (orientation == PatientOrientation.HeadFirstSupine | orientation == PatientOrientation.FeetFirstSupine) chkOrientation = 1;
            else if (orientation == PatientOrientation.HeadFirstProne | orientation == PatientOrientation.FeetFirstProne) chkOrientation = -1;


            string FileFolder = @"\\Vmstbox161\va_data$\ProgramData\Vision\PublishedScripts\PhtonCouchModel";
            string FilePathCI = System.IO.Path.Combine(new string[] { FileFolder, "CouchInterior.csv" });
            string FilePathCS = System.IO.Path.Combine(new string[] { FileFolder, "CouchSurface.csv" });

            scriptContext.Patient.BeginModifications();
            Structure Marker = SS.Structures.FirstOrDefault(e => e.DicomType == "MARKER");
            double final = Marker.CenterPoint.y;
            //if (SS.Structures.FirstOrDefault(s => s.DicomType == "EXTERNAL") is null)
            //{
            //    var BodyPar = SS.GetDefaultSearchBodyParameters();
            //    SS.CreateAndSearchBody(BodyPar);
            //}

            ////Find the BODY and decide the lowest x,y,z first
            //Structure BODY = SS.Structures.Where(s => s.DicomType == "EXTERNAL").FirstOrDefault();
            //List<VVector> Temp = new List<VVector>();
            //for (int i = 0; i < SI.ZSize; i++)
            //{
            //    foreach (VVector[] vectors in BODY.GetContoursOnImagePlane(i))
            //    {
            //        foreach (VVector v in vectors)
            //        {
            //            Temp.Add(new VVector(v.x, v.y, v.z));
            //        }
            //    }
            //}
            //VVector Ymax = Temp.Where(s => s.y.Equals(Temp.Max(p => p.y))).FirstOrDefault();

            //Find center X
            VVector Start = new VVector(700, 0, 0);
            VVector Stop = new VVector(-700, 0, 0);
            double[] PreallocatedBuffer = new double[1000];
            ImageProfile XProfile = SI.GetImageProfile(Start, Stop, PreallocatedBuffer);
            double X2 = XProfile.Where(p => !Double.IsNaN(p.Value)).Max(p => p.Position.x);
            double X1 = XProfile.Where(p => !Double.IsNaN(p.Value)).Min(p => p.Position.x);
            double Xcenter = (X2 + X1) / 2;
            double Xborder = Math.Abs(X2 - X1);

            //Find center Y
            Start = new VVector(Xcenter, 700* chkOrientation, 0);
            Stop = new VVector(Xcenter, -700* chkOrientation, 0);
            double[] YPreallocatedBuffer = new double[1000];
            ImageProfile YProfile = SI.GetImageProfile(Start, Stop, YPreallocatedBuffer);
            double Y2 = YProfile.Where(p => !Double.IsNaN(p.Value)).Max(p => p.Position.y);
            double Y1 = YProfile.Where(p => !Double.IsNaN(p.Value)).Min(p => p.Position.y);
            double Ycenter = (Y2 + Y1) / 2;
            double Yborder = Math.Abs(X2 - X1);

            //Find Y line edge near 53cm
            List<double> YHU_Diff = new List<double>();
            List<double> YLocation = new List<double>();
            for (int i = 1; i < Convert.ToInt32(SI.YSize * SI.YRes / 2); i++)
            {
                VVector __Start = new VVector((-SI.XSize * SI.XRes / 2) + Xcenter, chkOrientation *((SI.YSize * SI.YRes / 2) - (i)) + Ycenter, 0);
                VVector __Stop = new VVector((SI.XSize * SI.XRes / 2) + Xcenter, chkOrientation * ((SI.YSize * SI.YRes / 2) - (i)) + Ycenter, 0);
                double[] __PreallocatedBuffer = new double[100];
                ImageProfile __Profile = SI.GetImageProfile(__Start, __Stop, __PreallocatedBuffer);
                double sum = 0;
                foreach (ProfilePoint x in __Profile.Where(p => !Double.IsNaN(p.Value)))
                {
                    sum += Math.Abs(x.Value - (-450));
                }
                YHU_Diff.Add(sum);
                YLocation.Add(chkOrientation * ((SI.YSize * SI.YRes / 2) - (i)) + Ycenter);
            }
            int index = new int();
            double FinalYcenter = new int();
            index = YHU_Diff.IndexOf(YHU_Diff.Min());
            FinalYcenter = YLocation.ElementAt(index);

            //Find the point with the highest slope from centerx, and check the distance near 47cm or 51cm
            VVector Couch1, Couch2, Couch3, Couch4 = new VVector();
            for (int i = 0; i<50; i++)
            {
                YHU_Diff.RemoveAt(index);
                YLocation.RemoveAt(index);
                index = YHU_Diff.IndexOf(YHU_Diff.Min());
                FinalYcenter = YLocation.ElementAt(index);

                VVector _Start = new VVector(-275 + Xcenter, FinalYcenter + chkOrientation*3, 0);
                VVector _Stop = new VVector(0 + Xcenter, FinalYcenter + chkOrientation*3, 0);
                double[] _PreallocatedBuffer = new double[1000];
                ImageProfile XProfile1 = SI.GetImageProfile(_Start, _Stop, _PreallocatedBuffer);
                Couch1 = FindHighestSlope(XProfile1);
                _Start = new VVector(0 + Xcenter, FinalYcenter + chkOrientation*3, 0);
                _Stop = new VVector(275 + Xcenter, FinalYcenter + chkOrientation*3, 0);
                ImageProfile XProfile2 = SI.GetImageProfile(_Start, _Stop, _PreallocatedBuffer);
                Couch2 = FindHighestSlope(XProfile2);

                _Start = new VVector(-275 + Xcenter, FinalYcenter, 0);
                _Stop = new VVector(0 + Xcenter, FinalYcenter, 0);
                ImageProfile XProfile3 = SI.GetImageProfile(_Start, _Stop, _PreallocatedBuffer);
                Couch3 = FindHighestSlope(XProfile3);
                _Start = new VVector(0 + Xcenter, FinalYcenter, 0);
                _Stop = new VVector(275 + Xcenter, FinalYcenter, 0);
                ImageProfile XProfile4 = SI.GetImageProfile(_Start, _Stop, _PreallocatedBuffer);
                Couch4 = FindHighestSlope(XProfile4);

                double CouchBorder1 = Math.Round(VVector.Distance(Couch1, Couch2) / 10);
                double CouchBorder2 = Math.Round(VVector.Distance(Couch3, Couch4) / 10);
                if ((CouchBorder1 == 52 && CouchBorder2 == 51)| (CouchBorder1 == 51 && CouchBorder2 == 49)  ) break;
                i++;
            }


            //Add Couch
            List<VVector> CSVVector = new List<VVector>();
            string[] TempFilelines = File.ReadAllLines(FilePathCS);
            {
                foreach (string line in TempFilelines)
                {
                    string[] splitLine = line.Split(',');
                    double x = Double.Parse(splitLine[0].Trim());
                    double y = Double.Parse(splitLine[1].Trim());
                    double z = Double.Parse(splitLine[2].Trim());
                    CSVVector.Add(new VVector(x, chkOrientation * y, z));
                }
            }
            double MMX = MaxMinDetect(CSVVector, orientation)[0]; double MMY = MaxMinDetect(CSVVector, orientation)[1];
            double ShiftX = -265 - MMX;
            double ShiftY = (FinalYcenter) - MMY;

            Structure CouchSurface = SS.Structures.FirstOrDefault(e => e.Id == "CouchSurface");
            if (CouchSurface != null) SS.RemoveStructure(CouchSurface);
            CouchSurface = SS.AddStructure("SUPPORT", "CouchSurface");
            for (int i = 0; i < Convert.ToInt32(SI.ZSize); i++)
            {
                CouchSurface.AddContourOnImagePlane(CSVVector.Select(v => new VVector(v.x + ShiftX, v.y + ShiftY, v.z)).ToArray(), i);
            }


            Array.Clear(TempFilelines, 0, TempFilelines.Length);
            CSVVector.Clear();
            TempFilelines = File.ReadAllLines(FilePathCI);
            {
                foreach (string line in TempFilelines)
                {
                    string[] splitLine = line.Split(',');
                    double x = Double.Parse(splitLine[0].Trim());
                    double y = Double.Parse(splitLine[1].Trim());
                    double z = Double.Parse(splitLine[2].Trim());
                    CSVVector.Add(new VVector(x, chkOrientation * y, z));
                }
            }
            Structure CouchInterior = SS.Structures.FirstOrDefault(e => e.Id == "CouchInterior");
            if (CouchInterior != null) SS.RemoveStructure(CouchInterior);
            CouchInterior = SS.AddStructure("SUPPORT", "CouchInterior");
            for (int i = 0; i < Convert.ToInt32(SI.ZSize); i++)
            {
                CouchInterior.AddContourOnImagePlane(CSVVector.Select(v => new VVector(v.x + ShiftX, v.y + ShiftY, v.z)).ToArray(), i);
            }
            CouchSurface.SegmentVolume = CouchSurface.SegmentVolume.Sub(CouchInterior.SegmentVolume);
            //CouchInterior.SegmentVolume = CouchInterior.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 0,0,0,0, 0.03, 0));
            CouchInterior.SetAssignedHU(-950);
            CouchSurface.SetAssignedHU(-550);

        }
        public double[] MaxMinDetect(List<VVector> VVectors, PatientOrientation Ori)
        {
            double[] Final = { VVectors[0].x, VVectors[0].y, VVectors[0].z };
            for (int i = 1; i < VVectors.Count(); i++)
            {
                Final[0] = Math.Min(VVectors[i].x, Final[0]); //Always get the maximum value
                if (Ori == PatientOrientation.HeadFirstSupine | Ori == PatientOrientation.FeetFirstSupine)
                {
                    Final[1] = Math.Min(VVectors[i].y, Final[1]);
                }
                else if (Ori == PatientOrientation.HeadFirstProne | Ori == PatientOrientation.FeetFirstProne)
                {
                    Final[1] = Math.Max(VVectors[i].y, Final[1]);
                }
                Final[2] = Math.Min(VVectors[i].z, Final[2]);
            }
            return Final;
        }

        public List<VVector> PeakDetect(ImageProfile Profile)
        {
            List<VVector> Peakpoint = new List<VVector>();
            double OverallMaximum = Profile.Where(p => !Double.IsNaN(p.Value)).Max(p => p.Value);
            double OverallMinimum = Profile.Where(p => !Double.IsNaN(p.Value)).Min(p => p.Value);

            double Maximum = Profile.Where(p => !Double.IsNaN(p.Value)).Max(p => p.Value) - OverallMinimum;
            double HalfTrend = Maximum / 2;
            for (int i = 1; i < Profile.Count() - 1; i++)
            {

                double ii = Profile[i].Value - OverallMinimum;
                double iadd = Profile[i + 1].Value - OverallMinimum;
                double iminus = Profile[i - 1].Value - OverallMinimum;
                if (!Double.IsNaN(ii) && ii > HalfTrend && (ii > iadd) && (ii > iminus))
                {
                    Peakpoint.Add(Profile[i].Position);
                }
            }
            return Peakpoint;
        }

        public static double ClosestTo(List<double> collection, double target)
        {
            // NB Method will return int.MaxValue for a sequence containing no elements.
            // Apply any defensive coding here as necessary.
            double closest = new double();
            var minDifference = double.MaxValue;
            foreach (var element in collection)
            {
                var difference = Math.Abs((long)element - target);
                if (minDifference > difference)
                {
                    minDifference = (double)difference;
                    closest = element;
                }
            }
            return closest;
        }

        public static VVector FindHighestSlope(ImageProfile collection)
        {
            VVector HighestSlope = new VVector();
            var minDifference = double.MinValue;
            for (int i = 1; i < collection.Count() - 1; i++)
            {
                var difference = Math.Abs((long)collection[i + 1].Value - collection[i].Value);
                if (difference > minDifference)
                {
                    minDifference = (double)difference;
                    HighestSlope = collection[i].Position;
                }
            }
            return HighestSlope;
        }
    }
}
