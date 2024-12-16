using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using MJ_Proc_Gen;
using Grasshopper.Kernel.Types;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Rhino.DocObjects;
using Grasshopper.Rhinoceros.Model;
using Rhino;

namespace MJ_Proc_Gen_gh
{
    public class MJ_Proc_Gen_gh : GH_Component
    {
        public MJ_Proc_Gen_gh()
          : base("Initiate MJ", "Init MJ",
            "Establishes main MJ session to work in. It may contain multiple spaces and rulesets.",
            "MJ_Proc_Gen", "MJ_Proc_Gen")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Spaces", "SP", "Named spaces to run rulesets on", GH_ParamAccess.list); //SPACES
            pManager.AddTextParameter("Rulesets", "RS", "Named rulesets to run on spaces. Input a string of a file path to a ruleset .xml file", GH_ParamAccess.list); //RULESETS
            pManager.AddBooleanParameter("Enable Debug", "ED", "Enable debug logging", GH_ParamAccess.item, false); //ENABLE DEBUG
            pManager.AddGenericParameter("Refresh", "RF", "Refresh MJ_Main. Recommend to use a button", GH_ParamAccess.item); //REFRESH
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("MJ_Main", "M", "Main MJ Session Instance", GH_ParamAccess.item); //MAIN
            pManager.AddTextParameter("Debug Log", "DL", "Debug Log", GH_ParamAccess.list); //DEBUG
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Space> spaceList = new List<Space>();
            List<string> ruleSetList = new List<string>();
            bool enableDebug = false;
            if (!DA.GetDataList<Space>("Spaces", spaceList)) return;
            if (!DA.GetDataList<string>("Rulesets", ruleSetList)) return;
            if (!DA.GetData("Enable Debug", ref enableDebug)) return;
            MJ_Main m = new MJ_Main(enableDebug);
            foreach (Space space in spaceList)
            {
                m.AddSpace(space);
            }
            foreach (string s in ruleSetList)
            {
                m.AddRuleSet(s);
            }
            DA.SetData("MJ_Main", m);
            DA.SetDataList("Debug Log", m.debug.DebugText);
        }
        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("37605361-4be5-421a-9a51-24deead9e143");
    }
    public class RunMJ_gh : GH_Component
    {
        public RunMJ_gh()
          : base("Run MJ", "Run MJ",
            "Runs a ruleset on a space",
            "MJ_Proc_Gen", "MJ_Proc_Gen")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("MJ_Main", "M", "Main MJ Session Instance", GH_ParamAccess.item); //MAIN
            pManager.AddTextParameter("Space", "SP", "Name of space to operate on", GH_ParamAccess.item); //SPACE
            pManager.AddTextParameter("Ruleset", "RS", "Name of ruleset to use", GH_ParamAccess.item); //RULESET
            pManager.AddIntegerParameter("Maximum Operations", "MO", "Maximum number of operations allowed. Overruns may occur due to multiple operations in one cycle", GH_ParamAccess.item); //MAX OPS
            pManager.AddIntegerParameter("Random Seed", "SD", "Seed for random number generation", GH_ParamAccess.item); //SEED
            pManager.AddGenericParameter("Refresh", "RF", "Refresh run process. Recommend to use a button", GH_ParamAccess.item); //REFRESH
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("MJ_Main", "M", "Main MJ Session Instance", GH_ParamAccess.item); //MAIN
            pManager.AddTextParameter("Debug Log", "DL", "Debug Log", GH_ParamAccess.list); //DEBUG
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MJ_Main m = null;
            string sName = null;
            string rName = null;
            GH_Integer maxOps = null;
            GH_Integer regSize = null;
            GH_Integer seed = null;
            if (!DA.GetData("MJ_Main", ref m)) return;
            if (!DA.GetData("Space", ref sName)) return;
            if (!DA.GetData("Ruleset", ref rName)) return;
            if (!DA.GetData("Maximum Operations", ref maxOps)) return;
            if (!DA.GetData("Random Seed", ref seed)) return;
            m.RunMJ(sName, rName, maxOps.Value, seed.Value);
            DA.SetData("MJ_Main", m);
            DA.SetDataList("Debug Log", m.debug.DebugText);
        }
        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("f0bce6ab-7c4a-4778-9100-3e8e4775b483");
    }
    public class OutputMJ_gh : GH_Component
    {
        public OutputMJ_gh()
          : base("Output MJ", "Output MJ",
            "Collects results of the MJ process",
            "MJ_Proc_Gen", "MJ_Proc_Gen")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("MJ_Main", "M", "Main MJ Session Instance", GH_ParamAccess.item); //MAIN
            pManager.AddTextParameter("Space", "SP", "Name of space to operate on", GH_ParamAccess.item); //SPACE
            pManager.AddIntegerParameter("Frame", "FR", "A specific iteration of the MJ process", GH_ParamAccess.item); //FRAME
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("MJ_Main", "M", "Main MJ Session Instance", GH_ParamAccess.item); //MAIN
            pManager.AddIntegerParameter("Operation Count", "OC", "Total operations performed", GH_ParamAccess.item); //OPCOUNT
            pManager.AddIntegerParameter("Frame Count", "FC", "Total number of frames", GH_ParamAccess.item); //TOTAL FRAMES
            pManager.AddTextParameter("Frame", "FR", "Text representation of a single frame of the MJ process", GH_ParamAccess.item); //FRAME
            pManager.AddTextParameter("All Frames", "AF", "Text representation of all frames of the MJ process", GH_ParamAccess.item); //ALL FRAMES
            pManager.AddTextParameter("Rule Run", "RR", "Rule run in the frame specified", GH_ParamAccess.item); //RULE RUN
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MJ_Main m = null;
            string sName = null;
            GH_Integer frameN = null;
            if (!DA.GetData("MJ_Main", ref m)) return;
            if (!DA.GetData("Space", ref sName)) return;
            if (!DA.GetData("Frame", ref frameN)) return;
            int frame = frameN.Value;
            Space space = m.Spaces[sName];
            string[] frameStrings = space.SpaceStateOutput.Split('&');
            DA.SetData("MJ_Main", m);
            DA.SetData("Operation Count", space.OpCount);
            DA.SetData("Frame Count", frameStrings.Length);
            DA.SetData("Frame", frameStrings[frame]);
            DA.SetData("All Frames", space.SpaceStateOutput);
            DA.SetData("Rule Run", space.RulesRun[frame]);
        }
        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("74171541-bdfe-4edf-877b-ba66dd42a702");
    }
    public class ModelFrame_gh : GH_Component
    {
        public ModelFrame_gh()
          : base("Model Frame", "Model Frame",
            "Models one frame of output",
            "MJ_Proc_Gen", "MJ_Proc_Gen")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Frame", "FR", "Specifies which frame to model", GH_ParamAccess.item); //FRAME
            pManager.AddIntegerParameter("X Size", "XS", "Size of space X dimension", GH_ParamAccess.item); //X SIZE
            pManager.AddIntegerParameter("Y Size", "YS", "Size of space Y dimension", GH_ParamAccess.item); //Y SIZE
            pManager.AddIntegerParameter("Z Size", "ZS", "Size of space Z dimension", GH_ParamAccess.item); //Z SIZE
            pManager.AddNumberParameter("Spacing", "SP", "Spacing of output grid", GH_ParamAccess.item); //SPACING
            pManager.AddGenericParameter("Samples", "SM", "Samples of geometry corresponding to certain cell states", GH_ParamAccess.list); //SAMPLES
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model Objects", "MO", "Model objects arranged according to the specified frame", GH_ParamAccess.list);
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string frame = null;
            GH_Integer xSizeN = null;
            GH_Integer ySizeN = null;
            GH_Integer zSizeN = null;
            GH_Number spN = null;
            List<Sample> sampleList = new List<Sample>();
            if (!DA.GetData("Frame", ref frame)) return;
            if (!DA.GetData("X Size", ref xSizeN)) return;
            if (!DA.GetData("Y Size", ref ySizeN)) return;
            if (!DA.GetData("Z Size", ref zSizeN)) return;
            if (!DA.GetData("Spacing", ref spN)) return;
            if (!DA.GetDataList<Sample>("Samples", sampleList)) return;
            int xSize = xSizeN.Value;
            int ySize = ySizeN.Value;
            int zSize = zSizeN.Value;
            double sp = spN.Value;
            List<ModelObject> objects = new List<ModelObject>();
            Dictionary<string, Sample> samples = new Dictionary<string, Sample>();
            foreach (Sample s in sampleList)
            {
                samples.Add(s.RefName, s);
            }
            for (int x = 0; x < xSize; x++)
            {
                for (int y = 0; y < ySize; y++)
                {
                    for (int z = 0; z < zSize; z++)
                    {
                        string key = Item(x, Row(y, Plane(z, frame)));
                        Point3d p = new Point3d(x * sp, y * sp, z * sp);
                        Sample s = samples[key];
                        if (s.Objects != null)
                        {
                            foreach (ModelObject o in s.Objects)
                            {
                                Guid roGuid = (Guid)o.Id;
                                RhinoObject ro = RhinoDoc.ActiveDoc.Objects.FindId(roGuid);
                                GeometryBase gDup = ro.DuplicateGeometry();
                                gDup.Translate(p.X - s.RefPoint.X, p.Y - s.RefPoint.Y, p.Z - s.RefPoint.Z);
                                ObjectAttributes aDup = ro.Attributes.Duplicate();
                                Guid roDupGuid = RhinoDoc.ActiveDoc.Objects.Add(gDup, aDup);
                                RhinoObject roDup = RhinoDoc.ActiveDoc.Objects.FindId(roDupGuid); //commit changes?
                                ModelObject mo = new ModelObject(roDup);
                                RhinoDoc.ActiveDoc.Objects.Delete(roDup);
                                objects.Add(mo);
                            }
                        }
                    }
                }
            }
            DA.SetDataList("Model Objects", objects);
        }
        public string Plane(int i, string frame)
        {
            return frame.Split('/')[i];
        }
        public string Row(int i, string plane)
        {
            return plane.Split(';')[i];
        }
        public string Item(int i, string row)
        {
            return row.Split(',')[i];
        }
        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("dac89711-f6eb-4cb7-bad3-b511d1231c70");
    }
    public class Sample : GH_Component
    {
        private string refName;
        public string RefName { get { return refName; } }
        private Point3d refPoint;
        public Point3d RefPoint { get { return refPoint; } }
        private List<ModelObject> objects;
        public List<ModelObject> Objects { get { return objects; } }
        public Sample()
          : base("Sample", "Sample",
            "Assigns geometry and a reference point to a type of cell state",
            "MJ_Proc_Gen", "MJ_Proc_Gen")
        {
        }
        public Sample(string refName, Point3d refPoint, List<ModelObject> objIn)
        {
            this.refName = refName;
            this.refPoint = refPoint;
            objects = new List<ModelObject>();
            if (objIn.Count > 0)
            {
                foreach (ModelObject o in objIn)
                {
                    objects.Add(o);
                }
            }
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Reference Name", "RN", "Reference name for one potential output cell state", GH_ParamAccess.item); //REF NAME
            pManager.AddPointParameter("Reference Point", "RP", "Reference point for aligning sample geometry", GH_ParamAccess.item);//REF POINT
            int objIndex = pManager.AddGenericParameter("Model Object", "MO", "Rhino Object to be used for cells of this state", GH_ParamAccess.list);
            pManager[objIndex].Optional = true;
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Sample", "SM", "Reference sample", GH_ParamAccess.item); //SAMPLE
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string rn = null;
            Point3d rp = new Point3d();
            List<GeometryBase> geom = new List<GeometryBase>();
            List<ModelObject> objects = new List<ModelObject>();
            if (!DA.GetData("Reference Name", ref rn)) return;
            if (!DA.GetData("Reference Point", ref rp)) return;
            DA.GetDataList<ModelObject>("Model Object", objects);
            DA.SetData("Sample", new Sample(rn, rp, objects));
        }
        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("a0951d7c-af1c-4010-9b06-78826eeaaf6e");
    }
    public class Space_gh : GH_Component
    {
        public Space_gh()
          : base("MJ Space", "MJ Space",
            "Creates a space for running MJ process",
            "MJ_Proc_Gen", "MJ_Proc_Gen")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "NA", "Name of space", GH_ParamAccess.item); //NAME
            pManager.AddIntegerParameter("X Size", "XS", "Size of space in X dimension", GH_ParamAccess.item); //X SIZE
            pManager.AddIntegerParameter("Y Size", "YS", "Size of space in Y dimension", GH_ParamAccess.item); //Y SIZE
            pManager.AddIntegerParameter("Z Size", "ZS", "Size of space in Z dimension", GH_ParamAccess.item); //Z SIZE
            pManager.AddTextParameter("Default State", "DS", "The initial state of all cells in this space", GH_ParamAccess.item); //DEFAULT STATE
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Space", "SP", "New space", GH_ParamAccess.item); //SPACE
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null;
            GH_Integer xSizeN = null;
            GH_Integer ySizeN = null;
            GH_Integer zSizeN = null;
            string defaultState = null;
            if (!DA.GetData("Name", ref name)) return;
            if (!DA.GetData("X Size", ref xSizeN)) return;
            if (!DA.GetData("Y Size", ref ySizeN)) return;
            if (!DA.GetData("Z Size", ref zSizeN)) return;
            if (!DA.GetData("Default State", ref defaultState)) return;
            int xs = xSizeN.Value;
            int ys = ySizeN.Value;
            int zs = zSizeN.Value;
            DA.SetData("Space", new Space(name, xs, ys, zs, defaultState));
        }
        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("b954d0af-b5c0-4f47-8b2e-407a1b278f05");
    }
}