using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;

namespace MJ_Proc_Gen
{
    public class MJ_Main //main singleton object for all MJ operations
    {
        private Dictionary<string, Space> spaces;
        public Dictionary<string, Space> Spaces { get { return spaces; } }
        private Dictionary<string, RuleSet> ruleSets;
        public Debug_Logger debug;
        public MJ_Main(bool enableDebug) //call from front end to construct main instance
        {
            debug = new Debug_Logger(enableDebug);
            spaces = new Dictionary<string, Space>();
            ruleSets = new Dictionary<string, RuleSet>();
            debug.DebugLine("Created MJ_Main");
        }
        public void AddSpace(string name, int sizeX, int sizeY, int sizeZ, string defaultState) //call from front end
        {
            if (spaces.ContainsKey(name))
            {
                spaces.Remove(name);
            }
            spaces.Add(name, new Space(name, sizeX, sizeY, sizeZ, defaultState, this));
            debug.DebugLine("Added space \"" + name + "\" to list.");
        }
        public void AddSpace(Space space) //call from front end
        {
            if (spaces.ContainsKey(space.Name))
            {
                spaces.Remove(space.Name);
            }
            spaces.Add(space.Name, space);
            debug.DebugLine("Added space \"" + space.Name + "\" to list.");
        }
        public void AddRuleSet(string path) //call from front end
        {
            RuleSet set = XMLInterpreter.ImportRuleSet(path, this);
            string name = set.Name();
            ruleSets.Add(name, set);
            debug.DebugLine("Added ruleset \"" + name + "\" to list.");
        }
        public void RunMJ (string spaceName, string ruleSetName, int maxOperations, int seed) //call from front end
        {
            Space space = spaces[spaceName];
            RuleSet rootRuleSet = ruleSets[ruleSetName];
            debug.DebugLine("Being RunMJ(). Running ruleset \"" + rootRuleSet.Name() + "\" on \"" + space.Name + "\"");
            space.Reset();
            rootRuleSet.ResetUses();
            Random rng = new Random(seed);
            RecursiveRunRuleSet(space, rootRuleSet, maxOperations, rng);
            debug.DebugLine("End RunMJ()");
        }
        private void RecursiveRunRuleSet(Space space, RuleSet ruleSet, int maxOps, Random rng)
        {
            debug.DebugLine("Ruleset \"" + ruleSet.Name() + "\" has run " + ruleSet.Uses() + " out of " + ruleSet.Limit() + " times. Space \"" + space.Name + "\" has received " + space.OpCount + " operations out of " + maxOps);
            if (ruleSet.LimitUnreached() && space.OpCount < maxOps) //DON'T FORGET TO INCREMENT USES FOR RULESETS!!!! Rule uses are incremented under ApplyRule(). opCount is incremented under FindRuleMatch() and ApplyRule()
            {
                ruleSet.IncrementUses();
                debug.DebugLine("Ruleset \"" + ruleSet.Name() + "\" run " + ruleSet.Uses() + " out of " + ruleSet.Limit());
                switch (ruleSet.Type)
                {
                    case "random": //Select a random rule or ruleset from the current ruleset to execute once. Repeat until no valid rules remain. //TODO: implement random rulesets
                        debug.DebugLine("Running as a random ruleset");
                        break;
                    case "sequence": //Execute each rule in a ruleset one time in order. When the end of the ruleset is reached, start over from the beginning. //TODO: implement sequence rulesets
                        debug.DebugLine("Running as a sequence ruleset");
                        break;
                    case "series": //Execute a rule as many times as possible, then move on to the next rule.
                        debug.DebugLine("Running as a series ruleset. There are " + ruleSet.ChildRuleSet.Count + " rules and ruleset.");
                        foreach (IRuleSet r in ruleSet.ChildRuleSet)
                        {
                            debug.DebugLine("Rule or ruleset \"" + r.Name() + "\"");
                            switch (r.IType())
                            {
                                case "Rule":
                                    debug.DebugLine("Running rule under current ruleset");
                                    if (r.LimitUnreached() && space.OpCount < maxOps)
                                    {
                                        RuleMatch rm = space.FindRuleMatch((Rule)r, rng, maxOps);
                                        while (r.LimitUnreached() && space.OpCount < maxOps && rm.MatchFound)
                                        {
                                            space.ApplyRule(rm);
                                            rm = space.FindRuleMatch((Rule)r, rng, maxOps);
                                        }
                                    }
                                    break;
                                case "RuleSet": //TODO: consider making child rulesets of series rulesets repeat as many times as possible
                                    debug.DebugLine("Running nested ruleset under current ruleset");
                                    RecursiveRunRuleSet(space, (RuleSet)r, maxOps, rng);
                                    break;
                                default:
                                    debug.DebugException("Unrecognized IType in RuleSet");
                                    break;
                            }
                        }
                        break;
                    default:
                        debug.DebugException("Invalid RuleSet Type \"" + ruleSet.Type + "\"");
                        break;
                }
            }
            else
            {
                debug.DebugLine("Limit reached for ruleset \"" + ruleSet.Name() + "\"");
            }
        }
    }
    public class Space //represents a space of cells to operation on using rules.
    {
        private MJ_Main main;
        public MJ_Main Main { get { return main; } }
        private string name;
        public string Name { get { return name; } }

        private int sizeX, sizeY, sizeZ;
        public int SizeX { get { return sizeX; } }
        public int SizeY { get { return sizeY; } }
        public int SizeZ {  get { return sizeZ; } }
        private string defaultState; //TODO: consider also accepting a heterogeneous cellGrid as the default state
        public string DefaultState { get { return defaultState; } }
        private int opCount;
        public int OpCount { get { return opCount; } }
        private Cell[,,] cellGrid;
        public Cell[,,] CellGrid { get { return cellGrid; } }
        private string spaceStateOutput;
        public string SpaceStateOutput { get { return spaceStateOutput; } }
        private int outputFrameCount;
        public int OutputFrameCount { get { return outputFrameCount; } }

        public Space(string name, int sizeX, int sizeY, int sizeZ, string defaultState, MJ_Main main)
        {
            this.main = main;
            this.name = name;
            this.sizeX = sizeX;
            this.sizeY = sizeY;
            this.sizeZ = sizeZ;
            this.defaultState = defaultState;
            opCount = 0;
            cellGrid = new Cell[sizeX, sizeY, sizeZ];
            this.main.debug.DebugLine("Creating space \"" + name + "\". Size: x: " + sizeX + ", y: " + sizeY + ", z: " + sizeZ);
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        cellGrid[x, y, z] = new Cell(x, y, z, defaultState, this, main);
                    }
                }
            }
            spaceStateOutput = "";
            outputFrameCount = 0;
            AppendOutput();
        }
        public RuleMatch FindRuleMatch(Rule rule, Random rng, int maxOps) //find a location in space where the given rule matches
        {
            opCount++;
            main.debug.DebugLine("FindRuleMatch() called. Checking if rule \"" + rule.Name() + "\" has a match. Operations count is " + OpCount);
            //create a randomly ordered queue of cells in space
            Queue<Cell> cq = new Queue<Cell>();
            List<Cell> cl = new List<Cell>();
            foreach (Cell c in cellGrid)
            {
                cl.Insert(rng.Next(0, cl.Count), c);
            }
            foreach (Cell c in cl)
            {
                cq.Enqueue(c); 
            }
            //for each cell, check the rule and all its rotations
            while (cq.Count > 0 && opCount < maxOps)
            {
                //opCount++;
                Cell cc = cq.Dequeue();
                main.debug.DebugLine("Checking rule \"" + rule.Name() + "\" at cell " + cc.X + "," + cc.Y + "," + cc.Z + ". Cells remaining for this rule queue: " + cq.Count);
                {
                    foreach (KeyValuePair<string, string[,,]> ruleIn in rule.StrIn) //TODO: CHECK SYMMETRIES AND OFFSETS FROM CENTER IN A RANDOM ORDER INSTEAD
                    {
                        main.debug.DebugLine("Trying symmetry \"" + ruleIn.Key + "\"");
                        int rx = ruleIn.Value.GetLength(0); //prefix r (rule) = the dimensions of the ruleIn array
                        int ry = ruleIn.Value.GetLength(1);
                        int rz = ruleIn.Value.GetLength(2);
                        int dx = Convert.ToInt32(Math.Floor((decimal)rx / 2)); //prefix d (down) = the middle index rounded down
                        int dy = Convert.ToInt32(Math.Floor((decimal)ry / 2));
                        int dz = Convert.ToInt32(Math.Floor((decimal)rz / 2));
                        int ux = Convert.ToInt32(Math.Ceiling((decimal)rx / 2)); //prefix u (up) = the middle index rounded up
                        int uy = Convert.ToInt32(Math.Ceiling((decimal)ry / 2));
                        int uz = Convert.ToInt32(Math.Ceiling((decimal)rz / 2));
                        List<int> xOffsets = new List<int>(); //lists to contain offsets for each dimension
                        List<int> yOffsets = new List<int>();
                        List<int> zOffsets = new List<int>();
                        xOffsets.Add(dx); //dx is always added
                        yOffsets.Add(dy);
                        zOffsets.Add(dz);
                        if (dx == ux) { xOffsets.Add(dx - 1); } //if dx and ux are equal, we know that the dimension is even, and we need to check 2 offsets for each even dimension
                        if (dy == uy) { yOffsets.Add(dy - 1); }
                        if (dz == uz) { zOffsets.Add(dz - 1); }
                        int maxCellMatches = rx * ry * rz;
                        int cellMatches = 0;
                        Cell[,,] matchArray = new Cell[rx, ry, rz];
                        foreach (int ox in xOffsets)
                        {
                            foreach (int oy in yOffsets)
                            {
                                foreach ( int oz in zOffsets)
                                {
                                    //main.debug.DebugLine("Searching for matches with offsets " + ox + "," + oy + "," + oz + " from cell " + cc.X + "," + cc.Y + "," + cc.Z);
                                    for (int x = -ox; x < rx - ox; x++)
                                    {
                                        for (int y = -oy; y < ry - oy; y++)
                                        {
                                            for (int z = -oz; z < rz - oz; z++)
                                            {
                                                int ccx = cc.X + x;
                                                int ccy = cc.Y + y;
                                                int ccz = cc.Z + z;
                                                int px = x + ox;
                                                int py = y + oy;
                                                int pz = z + oz;
                                                bool xInRange = ccx >= 0 && ccx < SizeX;
                                                bool yInRange = ccy >= 0 && ccy < SizeY;
                                                bool zInRange = ccz >= 0 && ccz < SizeZ;
                                                if (xInRange && yInRange && zInRange)
                                                {
                                                    //main.debug.DebugLine("Cell state at cell " + ccx + "," + ccy + "," + ccz + " is \"" + CellGrid[ccx, ccy, ccz].State + "\" and corresponding rule state at " + px + "," + py + "," + pz + " is \"" + ruleIn.Value[px, py, pz] + "\"");
                                                    if (CellGrid[ccx, ccy, ccz].State == ruleIn.Value[px, py, pz] || ruleIn.Value[px, py, pz] == "*")
                                                    {
                                                        cellMatches++;
                                                        matchArray[px, py, pz] = CellGrid[ccx, ccy, ccz];
                                                        //main.debug.DebugLine("cellMatches = " + cellMatches + " and maxCellMatches = " + maxCellMatches);
                                                        if (cellMatches == maxCellMatches)
                                                        {
                                                            main.debug.DebugLine("Match found");
                                                            return new RuleMatch(matchArray, rule, ruleIn.Key);
                                                        }
                                                        else { continue; }
                                                    }
                                                    else
                                                    {
                                                        main.debug.DebugLine("Match failed: mismatch");
                                                        goto endMatchLoop;
                                                    }
                                                }
                                                else
                                                {
                                                    main.debug.DebugLine("Match failed: out of bounds");
                                                    goto endMatchLoop;
                                                }
                                            }
                                        }
                                    }
                                    main.debug.DebugLine("Matching cycle ended without finding a failure");
                                    endMatchLoop: cellMatches = 0;
                                }
                            }
                        }
                    }
                }
            }
            main.debug.DebugLine("Match not found");
            return new RuleMatch(rule);
        }
        public void ApplyRule(RuleMatch rm) //apply a matched rule to a region of space
        {
            opCount++;
            main.debug.DebugLine("ApplyRuleMatch() called. Operations count is " + OpCount);
            for (int x = 0; x < rm.Region.GetLength(0); x++)
            {
                for (int y = 0; y < rm.Region.GetLength(1); y++)
                {
                    for (int z = 0; z < rm.Region.GetLength(2); z++)
                    {
                        if (rm.BaseRule.StrOut[rm.TransformKey][x, y, z] != "*")
                        {
                            rm.Region[x, y, z].SetCellState(rm.BaseRule.StrOut[rm.TransformKey][x, y, z]);
                        }
                    }
                }
            }
            rm.BaseRule.IncrementUses();
            AppendOutput();
        }
        public void AppendOutput() //Encodes the current states of all cells in this space to a string and appends it to spaceStateOutput
        {
            outputFrameCount++;
            main.debug.DebugLine("Output frame count: " +  outputFrameCount);
            string s = "";
            if (spaceStateOutput != "") { s += "&"; }
            for (int z = 0; z < SizeZ; z++)
            {
                for (int y = 0; y < SizeY; y++)
                {
                    for (int x = 0; x <  SizeX; x++)
                    {
                        s += cellGrid[x, y, z].State;
                        if (x < SizeX - 1) { s += ","; }
                    }
                    if (y < SizeY - 1) { s += ";"; }
                }
                if (z < SizeZ - 1) { s += "/"; }
            }
            spaceStateOutput += s;
        }
        public void Reset() //resets the space to all default values
        {
            main.debug.DebugLine("Resetting space");
            opCount = 0;
            foreach (Cell c in cellGrid)
            {
                c.Reset(this);
            }
            spaceStateOutput = "";
            outputFrameCount = 0;
            AppendOutput();
        }
    }
    public interface IRuleSet //allows both Rules and RuleSets to exist in the same list, and contains some of their common properties
    {
        string IType(); //Stores whether this is a rule or a ruleset, useful for when executing the ruleset
        int Limit(); //The total number of times this rule or ruleset may be run. Limit = -1 for unlimited runs
        int Uses(); //The total number of times this rule has been used so far
        void ResetUses();
        void IncrementUses(); //Add one to the number of times used
        bool LimitUnreached(); //return true if Uses < Limit
        string Name();
    }
    public class RuleSet : IRuleSet //an ordered tree of rulesets and rules
    {
        private MJ_Main main;
        public MJ_Main MJ_Main { get { return main; } }
        private string iType;
        public string IType() { return iType; }
        private int limit;
        public int Limit() { return limit; }
        private int uses;
        public int Uses() { return uses; }
        public void ResetUses() { RecursiveResetUses(); }
        public void IncrementUses() { uses++; }
        public bool LimitUnreached()
        {
            if (Limit() >= 0)
            {
                if (Uses() < Limit()) { return true; }
                else { return false; }
            }
            return true;
        }
        private bool repeat; //IS THIS NECESSARY? Can this be handled by the ruleset type?
        public bool Repeat () { return repeat; } //Determines behavior for this ruleset when nested inside another one. If true, the ruleset will repeat until its rules are exhausted before continuing with its parent ruleset. If not, it will run once before returning to its parent. This cannot be true for root rulesets.
        private string name;
        public string Name() { return name; }
        private RuleSet parentRuleSet;
        public RuleSet ParentRuleSet { get { return parentRuleSet; } }
        private List<IRuleSet> childrenRuleSets;
        public List<IRuleSet> ChildRuleSet { get { return childrenRuleSets; } set { childrenRuleSets = value; } }
        private string type;
        public string Type { get { return type; } }
        public bool IsRoot { get { return parentRuleSet == null; } }
        /*public RuleSet(string name, List<IRuleSet> children, string ruleSetType, MJ_Main main) //constructor only for the root ruleset: parentRuleset is set to null
        {
            this.main = main;
            iType = "RuleSet";
            this.name = name;
            parentRuleSet = null;
            childrenRuleSets = children;
            type = ruleSetType;
            limit = -1;
            uses = 0;
            repeat = false;
        }*/
        public RuleSet(string name, List<IRuleSet> children, string ruleSetType, int limit, MJ_Main main) //constructor only for the root ruleset with a specified limit
        {
            this.main = main;
            iType = "RuleSet";
            this.name = name;
            parentRuleSet = null;
            childrenRuleSets = children;
            type = ruleSetType;
            this.limit = limit;
            uses = 0;
            repeat = false;
        }
        /*public RuleSet(string name, RuleSet parent, List<IRuleSet> children, string ruleSetType, bool repeat, MJ_Main main) //constructor for all child rulesets
        {
            this.main = main;
            iType = "RuleSet";
            this.name = name;
            parentRuleSet = parent;
            childrenRuleSets = children;
            type = ruleSetType;
            limit= -1;
            uses = 0;
            this.repeat = repeat;
        }*/
        public RuleSet(string name, RuleSet parent, List<IRuleSet> children, string ruleSetType, bool repeat, int limit, MJ_Main main) //constructor for all child rulesets with a specified limit
        {
            this.main = main;
            iType = "RuleSet";
            this.name = name;
            parentRuleSet = parent;
            childrenRuleSets = children;
            type = ruleSetType;
            this.limit = limit;
            uses = 0;
            this.repeat = repeat;
        }
        private void RecursiveResetUses()
        {
            uses = 0;
            foreach (IRuleSet ir in childrenRuleSets) ir.ResetUses();
        }
    }
    public class Rule : IRuleSet //a rule that determines how certain patterns of cells are changed
    {
        private MJ_Main main;
        public MJ_Main MJ_Main { get { return main; } }
        private string iType;
        public string IType() { return iType; }
        private int limit;
        public int Limit() { return limit; }
        private int uses;
        public int Uses() { return uses; }
        public void ResetUses() { uses = 0; }
        public void IncrementUses() { uses++; }
        public bool LimitUnreached()
        {
            if (Limit() >= 0)
            {
                if (Uses() < Limit()) { return true; }
                else { return false; }
            }
            return true;
        }
        private string name;
        public string Name() { return name; }
        private Dictionary<string, string[,,]> strIn;
        public Dictionary<string, string[,,]> StrIn { get { return strIn; } }
        private Dictionary<string, string[,,]> strOut;
        public Dictionary<string, string[,,]> StrOut { get { return strOut; } }
        private bool[] symmetries;
        public bool[] Symmetries;
        private RuleSet parentRuleSet;
        public RuleSet ParentRuleSet { get { return parentRuleSet; } }
        public Rule(string name, Dictionary<string, string[,,]> strIn, Dictionary<string, string[,,]> strOut, bool[] symmetries, int limit, RuleSet parentRuleSet, MJ_Main mj_main)
        {
            this.main = mj_main;
            iType = "Rule";
            this.name = name;
            this.strIn = strIn;
            this.strOut = strOut;
            this.symmetries = symmetries;
            this.limit = limit;
            uses = 0;
            this.parentRuleSet = parentRuleSet;
            GenerateRotationsReflections(strIn["base"], symmetries);
            GenerateRotationsReflections(strOut["base"], symmetries);
        }
        private void GenerateRotationsReflections(string[,,] baseRule, bool[] sym) //TODO: IMPLEMENT ROTATIONS AND REFLECTIONS
        {

        }
    }
    public struct RuleMatch //conveys the necessary information to apply a matched rule
    {
        public bool MatchFound { get; }
        public Cell[,,] Region { get; }
        public Rule BaseRule { get; }
        public string TransformKey { get; }
        public RuleMatch(Cell[,,] region, Rule baseRule, string transformKey) //use this constructor when a match is found
        {
            Region = region;
            BaseRule = baseRule;
            TransformKey = transformKey;
            MatchFound = true;
        }
        public RuleMatch(Rule baseRule) //use this constructor when a match is not found
        {
            Region = null;
            this.BaseRule = baseRule;
            TransformKey = null;
            MatchFound = false;
        }
    }
    public class Cell //one cell in a space
    {
        private MJ_Main main;
        public MJ_Main MJ_Main { get { return main; } }
        private int x, y, z; //coordinates of this cell in MJ_Space
        public int X { get { return x; } }
        public int Y { get { return y; } }
        public int Z { get { return z; } }
        private string state; //do not modify directly; use SetCellState()
        public string State { get { return state; } }
        private Space space;
        public Cell(int x, int y, int z, string state, Space space, MJ_Main main)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.state = state;
            this.space = space;
            this.main = main;
            //mj_main.debug.DebugLine("Created new cell in " + mj_space.Name + " at " + X + ", " +  Y + ", " + Z);
        }
        public void Reset(Space s)
        {
            state = s.DefaultState;
        }
        public void SetCellState(string s)
        {
            if (RGX.RIsMatch(s, RGX.AlphaNumeric))
            {
                state = s;
                main.debug.DebugLine("Cell " + X + ", " + Y + ", " + Z + " in \"" + space.Name + "\" state set to \"" + State + "\"");
            }
            else
            {
                main.debug.DebugLine("Cell " + X + ", " + Y + ", " + Z + " in \"" + space.Name + "\" state not set: invalid state name");
            }
        }
    }
    public class Debug_Logger //contains lines of text for debugging
    {
        private bool debugEnabled;
        //private string path;
        private List<string> debugText;
        public List<string> DebugText { get { return debugText; } }
        public Debug_Logger(bool enableDebug)
        {
            debugEnabled = enableDebug;
            debugText = new List<string>();
        }
        public void DebugLine(string line)
        {
            if (debugEnabled)
            {
                debugText.Add(line);
            }
        }
        public void DebugException(string line)
        {
            if (debugEnabled)
            {
                debugText.Add(line);
                throw new Exception("EXCEPTION: " + line);
            }
        }
    }
    public static class RGX //use this for typical regex comparisons
    {
        public static string AlphaNumeric = "^[a-zA-Z0-9]+$";
        public static bool RIsMatch(string str, string rgx)
        {
            Regex r = new Regex(rgx);
            return r.IsMatch(str);
        }
    }
    public static class XMLInterpreter //use for translating input xmls into rules and rulesets
    {
        public static RuleSet ImportRuleSet(string path, MJ_Main main)
        {
            XmlDocument doc = new XmlDocument(); //new xml doc
            doc.Load(path); //load xml from file
            XmlNode rootNode = doc.DocumentElement; //get xml root node
            string name = rootNode.Attributes["name"].Value; //get name of root node
            List<IRuleSet> ruleList = new List<IRuleSet>(); //create empty list to be filled with rules and rulesets
            main.debug.DebugLine("Importing ruleset " + name + " with " + rootNode.ChildNodes.Count + " children"); //report to debug log
            int limit = -1;
            if (rootNode.Attributes["limit"] != null)
            {
                limit = Convert.ToInt32(rootNode.Attributes["limit"].Value);
            }
            RuleSet rootSet = new RuleSet(name, ruleList, rootNode.Attributes["type"].Value, limit, main); //create root ruleset from root node
            rootSet.ChildRuleSet = RecursiveImportRuleSet(rootSet, rootNode, main); //recursively populate child rules and rulesets
            main.debug.DebugLine("Imported ruleset " + name); //report to debug log
            return rootSet; //return complete ruleset
        }
        private static List<IRuleSet> RecursiveImportRuleSet(RuleSet rs, XmlNode xn, MJ_Main main) //create a tree of rules and rulesets
        {
            List<IRuleSet> ruleList = new List<IRuleSet>();
            foreach (XmlNode ruleNode in xn.ChildNodes)
            {
                main.debug.DebugLine("Adding node \"" + ruleNode.Attributes["name"].Value + "\" as a " + ruleNode.Name + " child of \"" + ruleNode.ParentNode.Attributes["name"].Value + "\"");
                switch (ruleNode.Name)
                {
                    case "rule":
                        ruleList.Add(RuleFromXmlNode(ruleNode, rs, main));
                        break;
                    case "ruleset":
                        int limit = -1;
                        if (ruleNode.Attributes["limit"] != null)
                        {
                            limit = Convert.ToInt32(ruleNode.Attributes["limit"].Value);
                        }
                        RuleSet newRuleSet = new RuleSet(ruleNode.Attributes["name"].Value, rs, new List<IRuleSet>(), ruleNode.Attributes["type"].Value, StrToBool(ruleNode.Attributes["repeat"].Value), limit, main);
                        newRuleSet.ChildRuleSet = RecursiveImportRuleSet(newRuleSet, ruleNode, main);
                        ruleList.Add(newRuleSet);
                        break;
                    default:
                        main.debug.DebugException("UNKNOWN RULE TYPE " + ruleNode.Name);
                        break;
                }
            }
            return ruleList;
        }
        private static Rule RuleFromXmlNode(XmlNode node, RuleSet parent, MJ_Main main)
        {
            string ruleIn = null;
            string ruleOut = null;
            string[] inOut = node.InnerText.Split('=');
            if (inOut.Length != 2)
            {
                main.debug.DebugException("Invalid rule text in rule: \"" + node.Attributes["name"].Value + "\"");
            }
            else
            {
                ruleIn = inOut[0];
                ruleOut = inOut[1];
            }
            bool[] sym = new bool[6]
            {
                StrToBool(node.Attributes["rotx"].Value),
                StrToBool(node.Attributes["roty"].Value),
                StrToBool(node.Attributes["rotz"].Value),
                StrToBool(node.Attributes["refx"].Value),
                StrToBool(node.Attributes["refy"].Value),
                StrToBool(node.Attributes["refz"].Value)
            };
            int limit = -1;
            if (node.Attributes["limit"] != null)
            {
                limit = Convert.ToInt32(node.Attributes["limit"].Value);
                main.debug.DebugLine("Rule limit is " + limit);
            }
            else
            {
                main.debug.DebugLine("Rule limit is null");
            }
            Rule r = new Rule(node.Attributes["name"].Value, stringTo3DStringArrayDic(ruleIn, sym, main), stringTo3DStringArrayDic(ruleOut, sym, main), sym, limit, parent, main);
            return r;
        }
        private static Dictionary<string, string[,,]> stringTo3DStringArrayDic(string s, bool[] sym, MJ_Main main)
        {
            string[] planes = s.Split('/');
            int sizeZ = planes.Length;
            string[] rowsY = planes[0].Split(';');
            int sizeY = rowsY.Length;
            string[] rowsX = rowsY[0].Split(',');
            int sizeX = rowsX.Length;
            string[] fullSplit = s.Split(new char[] { '/', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[,,] baseRuleArray = new string[sizeX, sizeY, sizeZ];
            Dictionary<string, string[,,]> ruleArrays = new Dictionary<string, string[,,]>();
            int i = 0;
            for (int z = 0; z < sizeZ; z++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        baseRuleArray[x, y, z] = fullSplit[i];
                        main.debug.DebugLine("rule cell " + x + ", " + y + ", " + z + " is " + fullSplit[i]);
                        i++;
                    }
                }
            }
            ruleArrays.Add("base", baseRuleArray);
            return ruleArrays;
        }
        private static bool StrToBool(string s)
        {
            switch (s)
            {
                case "t":
                    return true;
                case "f":
                    return false;
                default:
                    throw new Exception("INVALID SYMMETRY NOTATION. MUST BE t OR f");
            }
        }
    }
}