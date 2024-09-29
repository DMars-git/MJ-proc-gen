using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MJ_Proc_Gen
{
    public class MJ_Main //main singleton object for all MJ operations
    {
        private Dictionary<string, Space> spaces;
        public Dictionary<string, Space> Spaces { get { return spaces; } }
        private Dictionary<string, RuleSet> ruleSets;
        public Dictionary<string, RuleSet> RuleSets { get { return ruleSets; } }
        public Debug_Logger debug;
        public MJ_Main(bool enableDebug) //call from front end to construct main instance
        {
            debug = new Debug_Logger(enableDebug);
            spaces = new Dictionary<string, Space>();
            ruleSets = new Dictionary<string, RuleSet>();
            debug.DebugLine("Created MJ_Main");
        }
        public void AddSpace(string name, int sizeX, int sizeY, int sizeZ, string defaultState) //call from front end to construct a space and add it
        {
            if (spaces.ContainsKey(name))
            {
                spaces.Remove(name);
            }
            spaces.Add(name, new Space(name, sizeX, sizeY, sizeZ, defaultState, this));
            debug.DebugLine("Added space \"" + name + "\" to list.");
        }
        public void AddSpace(Space space) //call from front end to add an already constructed space
        {
            if (spaces.ContainsKey(space.Name))
            {
                spaces.Remove(space.Name);
            }
            spaces.Add(space.Name, space);
            space.SetMain(this);
            debug.DebugLine("Added space \"" + space.Name + "\" to list. Size: x: \"" + space.SizeX + "\", y: \"" + space.SizeY + "\", z: \"" + space.SizeZ);
        }
        public void AddRuleSet(string path) //call from front end to add a ruleset from an .xml file
        {
            RuleSet set = XMLInterpreter.ImportRuleSet(path, this);
            string name = set.Name();
            ruleSets.Add(name, set);
            debug.DebugLine("Added ruleset \"" + name + "\" to list.");
        }
        public void RunMJ (string spaceName, string ruleSetName, int maxOperations, int seed) //call from front end to run a ruleset on a space
        {
            Space space = spaces[spaceName];
            RuleSet rootRuleSet = ruleSets[ruleSetName];
            debug.DebugLine("Begin RunMJ(). Running ruleset \"" + rootRuleSet.Name() + "\" on \"" + space.Name + "\"");
            space.Reset();
            rootRuleSet.ResetUses();
            Random rng = new Random(seed);
            RecursiveRunRuleSet(space, rootRuleSet, maxOperations, rng);
            debug.DebugLine("End RunMJ()");
        }
        private void RecursiveRunRuleSet(Space space, RuleSet ruleSet, int maxOps, Random rng)
        {
            debug.DebugLine("Ruleset \"" + ruleSet.Name() + "\" has run " + ruleSet.Uses() + " out of " + ruleSet.Limit() + " times. Space \"" + space.Name + "\" has received " + space.OpCount + " operations out of " + maxOps);
            if (ruleSet.IsFinishedInRound()) //TODO: What about rulesets meant to be repeated?
            {
                ruleSet.ResetFinishedInRound();
                return;
            }
            if (ruleSet.LimitUnreached() && space.OpCount < maxOps) //Rule uses are incremented under ApplyRule(). opCount is incremented under FindRuleMatch() and ApplyRule()
            {
                ruleSet.IncrementUses();
                debug.DebugLine("Ruleset \"" + ruleSet.Name() + "\" run " + ruleSet.Uses() + " out of " + ruleSet.Limit());
                switch (ruleSet.RType())
                {
                    case "random": //Select a random rule or ruleset from the current ruleset to execute once. Repeat until no valid rules remain.
                        debug.DebugLine("Running as a random ruleset");
                        while (ruleSet.LimitUnreached() && !ruleSet.IsFinishedInRound())
                        {
                            ruleSet.IncrementUses();
                            IRuleSet rr = ruleSet.ChildRuleSet[rng.Next(ruleSet.ChildRuleSet.Count)];
                            debug.DebugLine("Rule or ruleset \"" + rr.Name() + "\"");
                            switch (rr.IType())
                            {
                                case "Rule":
                                    debug.DebugLine("Running rule under current ruleset");
                                    switch (rr.RType())
                                    {
                                        case "single":
                                            RunRuleSingleOnce(rr, space, maxOps, rng);
                                            break;
                                        case "parallel":
                                            RunRuleParallel(rr, space, maxOps, rng);
                                            break;
                                    }
                                    break;
                                case "RuleSet":
                                    debug.DebugLine("Running nested ruleset under current random ruleset");
                                    RecursiveRunRuleSet(space, (RuleSet)rr, maxOps, rng);
                                    break;
                                default:
                                    debug.DebugException("Unrecognized IType in RuleSet");
                                    break;
                            }
                        }
                        break;
                    case "sequence": //Execute each rule in a ruleset one time in order. When the end of the ruleset is reached, start over from the beginning.
                        debug.DebugLine("Running as a sequence ruleset");
                        while (ruleSet.LimitUnreached() && !ruleSet.IsFinishedInRound())
                        {
                            foreach (IRuleSet r in ruleSet.ChildRuleSet)
                            {
                                debug.DebugLine("Rule or ruleset \"" + r.Name() + "\"");
                                switch (r.IType())
                                {
                                    case "Rule":
                                        debug.DebugLine("Running rule under current ruleset");
                                        switch (r.RType())
                                        {
                                            case "single":
                                                RunRuleSingleOnce(r, space, maxOps, rng);
                                                break;
                                            case "parallel":
                                                RunRuleParallel(r, space, maxOps, rng);
                                                break;
                                        }
                                        break;
                                    case "RuleSet": //TODO: Should rulesets nested under a sequence ruleset be forced to only run once?
                                        debug.DebugLine("Running nested ruleset under current sequence ruleset");
                                        RecursiveRunRuleSet(space, (RuleSet)r, maxOps, rng);
                                        break;
                                    default:
                                        debug.DebugException("Unrecognized IType in RuleSet");
                                        break;
                                }
                            }
                        }
                        break;
                    case "retrace": //Execute the first rule until no matches are left. Then start checking the next rule, but at each cycle, check if the previous rules have a valid solution again.
                        debug.DebugLine("Running as a retrace ruleset");
                        int i = 0;
                        while (i < ruleSet.ChildRuleSet.Count && ruleSet.LimitUnreached() && !ruleSet.IsFinishedInRound())
                        {
                            debug.DebugLine("i = " + i);
                            IRuleSet r = ruleSet.ChildRuleSet[i];
                            debug.DebugLine("Rule or ruleset \"" + r.Name() + "\"");
                            bool b = false;
                            switch (r.IType())
                            {
                                case "Rule":
                                    debug.DebugLine("Running rule under current ruleset");
                                    switch (r.RType())
                                    {
                                        case "single":
                                            if (RunRuleSingleOnce(r, space, maxOps, rng)) { b = true; }
                                            break;
                                        case "parallel":
                                            if (RunRuleParallel(r, space, maxOps, rng)) { b = true; }
                                            break;
                                    }
                                    break;
                                case "RuleSet":
                                    debug.DebugLine("Running nested ruleset under current retrace ruleset");
                                    string before = space.SpaceStateOutput;
                                    RecursiveRunRuleSet(space, (RuleSet)r, maxOps, rng);
                                    if (space.SpaceStateOutput != before) { b = true; }
                                    break;
                                default:
                                    debug.DebugException("Unrecognized IType in RuleSet");
                                    break;
                            }
                            if (b)
                            {
                                i = 0;
                                continue;
                            }
                            i++;
                        }
                        break;
                    case "series": //Execute a rule as many times as possible, then move on to the next rule.
                        debug.DebugLine("Running as a series ruleset. There are " + ruleSet.ChildRuleSet.Count + " rules and rulesets.");
                        foreach (IRuleSet r in ruleSet.ChildRuleSet)
                        {
                            debug.DebugLine("Rule or ruleset \"" + r.Name() + "\"");
                            switch (r.IType())
                            {
                                case "Rule":
                                    debug.DebugLine("Running rule under current ruleset");
                                    switch (r.RType())
                                    {
                                        case "single":
                                            RunRuleSingleRepeat(r, space, maxOps, rng);
                                            break;
                                        case "parallel":
                                            RunRuleParallel(r, space, maxOps, rng);
                                            break;
                                    }
                                    break;
                                case "RuleSet":
                                    debug.DebugLine("Running nested ruleset under current series ruleset");
                                    RecursiveRunRuleSet(space, (RuleSet)r, maxOps, rng);
                                    break;
                                default:
                                    debug.DebugException("Unrecognized IType in RuleSet");
                                    break;
                            }
                        }
                        break;
                    default:
                        debug.DebugException("Invalid RuleSet Type \"" + ruleSet.RType() + "\"");
                        break;
                }
                if (ruleSet.Repeat())
                {
                    RecursiveRunRuleSet(space, ruleSet, maxOps, rng);
                }
            }
            else
            {
                debug.DebugLine("Limit reached for ruleset \"" + ruleSet.Name() + "\"");
            }
        }
        private bool RunRuleSingleRepeat(IRuleSet r, Space space, int maxOps, Random rng)
        {
            if (r.LimitUnreached() && space.OpCount < maxOps)
            {
                RuleMatch rm = space.FindRuleMatches((Rule)r, rng, maxOps, false)[0];
                bool ret = false;
                if (rm.MatchFound) { ret = true; }
                while (r.LimitUnreached() && space.OpCount < maxOps && rm.MatchFound)
                {
                    space.ApplyRuleMatch(rm);
                    rm = space.FindRuleMatches((Rule)r, rng, maxOps, false)[0];
                }
                return ret;
            }
            return false;
        }
        private bool RunRuleSingleOnce(IRuleSet r, Space space, int maxOps, Random rng)
        {
            if (r.LimitUnreached() && space.OpCount < maxOps)
            {
                RuleMatch rm = space.FindRuleMatches((Rule)r, rng, maxOps, false)[0];
                if (r.LimitUnreached() && space.OpCount < maxOps && rm.MatchFound)
                {
                    space.ApplyRuleMatch(rm);
                    return true;
                }
            }
            return false;
        }
        private bool RunRuleParallel(IRuleSet r, Space space, int maxOps, Random rng)
        {
            if (r.LimitUnreached() && space.OpCount < maxOps)
            {
                List<RuleMatch> rml = space.FindRuleMatches((Rule)r, rng, maxOps, true);
                if (r.LimitUnreached() && space.OpCount < maxOps && rml[0].MatchFound)
                {
                    debug.DebugLine("Found " + rml.Count + " matches for parallel rule " + r.Name());
                    foreach (RuleMatch rm in rml)
                    {
                        space.IncrementOpCount();
                        space.ApplyRuleMatch(rm);
                    }
                    space.AppendOutput();
                    return true;
                }
            }
            return false;
        }
    }
    public class Space //represents a space of cells to operation on using rules.
    {
        private MJ_Main main;
        public MJ_Main Main { get { return main; } }
        public void SetMain(MJ_Main m) { main = m; }
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
        public void IncrementOpCount() { opCount++; }
        private Cell[,,] cellGrid;
        public Cell[,,] CellGrid { get { return cellGrid; } }
        private string spaceStateOutput;
        public string SpaceStateOutput { get { return spaceStateOutput; } }
        private int outputFrameCount;
        public int OutputFrameCount { get { return outputFrameCount; } }
        public Space(string name, int sizeX, int sizeY, int sizeZ, string defaultState) //constructor if MJ_Main is NOT known
        {
            main = null;
            this.name = name;
            this.sizeX = sizeX;
            this.sizeY = sizeY;
            this.sizeZ = sizeZ;
            this.defaultState = defaultState;
            opCount = 0;
            cellGrid = new Cell[sizeX, sizeY, sizeZ];
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        cellGrid[x, y, z] = new Cell(x, y, z, defaultState, this);
                    }
                }
            }
            spaceStateOutput = "";
            outputFrameCount = 0;
            AppendOutput();
        }

        public Space(string name, int sizeX, int sizeY, int sizeZ, string defaultState, MJ_Main main) //constructor if MJ_Main is known
        {
            this.main = main;
            this.name = name;
            this.sizeX = sizeX;
            this.sizeY = sizeY;
            this.sizeZ = sizeZ;
            this.defaultState = defaultState;
            opCount = 0;
            cellGrid = new Cell[sizeX, sizeY, sizeZ];
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        cellGrid[x, y, z] = new Cell(x, y, z, defaultState, this);
                    }
                }
            }
            spaceStateOutput = "";
            outputFrameCount = 0;
            AppendOutput();
        }
        public List<RuleMatch> FindRuleMatches(Rule rule, Random rng, int maxOps, bool findAll) //Find locations in space where the given rule matches
        {
            IncrementOpCount();
            List<RuleMatch> ruleMatches= new List<RuleMatch>();
            main.debug.DebugLine("FindRuleMatches() called. Checking if rule \"" + rule.Name() + "\" has a match. Operations count is " + OpCount);
            //create a randomly ordered queue of cells in space
            Queue<Cell> cq = Shuffle.RandomQueue<Cell>(cellGrid, rng);
            //for each cell, check the rule and all its rotations
            while (cq.Count > 0 && opCount < maxOps)
            {
                //IncrementOpCount();
                Cell cc = cq.Dequeue();
                main.debug.DebugLine("Checking rule \"" + rule.Name() + "\" at cell " + cc.X + "," + cc.Y + "," + cc.Z + ". Cells remaining for this rule queue: " + cq.Count);
                {
                    List<string> keys = new List<string>();
                    foreach (KeyValuePair<string, string[,,]> k in rule.StrIn)
                    {
                        keys.Add(k.Key);
                    }
                    List<string> keysRandom = Shuffle.RandomList(keys, rng);
                    foreach (string key in keysRandom)
                    {
                        string[,,] strArray = rule.StrIn[key];
                        main.debug.DebugLine("Trying symmetry \"" + key + "\"");
                        int rx = strArray.GetLength(0); //prefix r (rule) = the dimensions of the ruleIn array
                        int ry = strArray.GetLength(1);
                        int rz = strArray.GetLength(2);
                        int maxCellMatches = rx * ry * rz;
                        int cellMatches = 0;
                        Cell[,,] matchArray = new Cell[rx, ry, rz];
                        for (int x = 0; x < rx; x++)
                        {
                            for (int y = 0; y < ry; y++)
                            {
                                for (int z = 0; z < rz; z++)
                                {
                                    int ccx = cc.X + x;
                                    int ccy = cc.Y + y;
                                    int ccz = cc.Z + z;
                                    bool xInRange = ccx >= 0 && ccx < SizeX;
                                    bool yInRange = ccy >= 0 && ccy < SizeY;
                                    bool zInRange = ccz >= 0 && ccz < SizeZ;
                                    if (xInRange && yInRange && zInRange)
                                    {
                                        //main.debug.DebugLine("Cell state at cell " + ccx + "," + ccy + "," + ccz + " is \"" + CellGrid[ccx, ccy, ccz].State + "\" and corresponding rule state at " + px + "," + py + "," + pz + " is \"" + ruleIn.Value[px, py, pz] + "\"");
                                        if (CellGrid[ccx, ccy, ccz].State == strArray[x, y, z] || strArray[x, y, z] == "*")
                                        {
                                            cellMatches++;
                                            matchArray[x, y, z] = CellGrid[ccx, ccy, ccz];
                                            //main.debug.DebugLine("cellMatches = " + cellMatches + " and maxCellMatches = " + maxCellMatches);
                                            if (cellMatches == maxCellMatches)
                                            {
                                                RuleMatch rm = new RuleMatch(matchArray, rule, key);
                                                if (!MatchOutputEqualsExistingState(rm))
                                                {
                                                    main.debug.DebugLine("Match found");
                                                    ruleMatches.Add(rm);
                                                    if (!findAll)
                                                    {
                                                        return ruleMatches;
                                                    }
                                                }
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
                    endMatchLoop: cellMatches = 0;
                    }
                }
            }
            if (ruleMatches.Count == 0)
            {
                main.debug.DebugLine("Match not found");
                ruleMatches.Add(new RuleMatch(rule));
                rule.FinishedInRound();
            }
            return ruleMatches;
        }
        public void ApplyRuleMatch(RuleMatch rm) //apply a matched rule to a region of space
        {
            main.debug.DebugLine("ApplyRuleMatch() called. Operations count is " + OpCount);
            for (int x = 0; x < rm.Region.GetLength(0); x++)
            {
                for (int y = 0; y < rm.Region.GetLength(1); y++)
                {
                    for (int z = 0; z < rm.Region.GetLength(2); z++)
                    {
                        if (rm.Rule.StrOut[rm.TransformKey][x, y, z] != "*")
                        {
                            Cell c = rm.Region[x, y, z];
                            c.SetCellState(rm.Rule.StrOut[rm.TransformKey][x, y, z]);
                            main.debug.DebugLine("Cell " + c.X + ", " + c.Y + ", " + c.Z + " in \"" + Name + "\" state set to \"" + c.State + "\"");
                        }
                    }
                }
            }
            rm.Rule.IncrementUses();
            if (rm.Rule.RType() != "parallel")
            {
                IncrementOpCount();
                AppendOutput();
            }
        }
        public bool MatchOutputEqualsExistingState(RuleMatch rm) //Checks whether applying the rulematch would result in a change to the space. We can use this to skip useless matches
        {
            for (int x = 0; x < rm.Region.GetLength(0); x++)
            {
                for (int y = 0; y < rm.Region.GetLength(1); y++)
                {
                    for (int z = 0; z < rm.Region.GetLength(2); z++)
                    {
                        if (rm.Rule.StrOut[rm.TransformKey][x, y, z] != "*" && rm.Region[x, y, z].State != rm.Rule.StrOut[rm.TransformKey][x, y, z])
                        {
                            return false;
                        }
                    }
                }
            }
            //main.debug.DebugLine("Useless rule found");
            return true;
        }
        public void AppendOutput() //Encodes the current states of all cells in this space to a string and appends it to spaceStateOutput
        {
            outputFrameCount++;
            //main.debug.DebugLine("Output frame count: " +  outputFrameCount);
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
        string RType(); //Stores the type of rule or ruleset
        int Limit(); //The total number of times this rule or ruleset may be run. Limit = -1 for unlimited runs
        int Uses(); //The total number of times this rule has been used so far
        void ResetUses(); //Reset the number of uses for this rule or ruleset
        void IncrementUses(); //Add one to the number of times used
        bool LimitUnreached(); //return true if Uses < Limit
        string Name(); //The name of the rule or ruleset
        bool IsFinishedInRound(); //Set to true if the space contains no matches for this rule. Or, if this is a ruleset, there were no matches for any of its child rules. This is used to stop the search for matches once no more matches remain.
        void ResetFinishedInRound(); //Reset all IsFinishedInRound for this ruleset and its child rules at the start of a RecursiveRunRuleSet cycle
        RuleSet ParentRuleSet();
    }
    public class RuleSet : IRuleSet //an ordered tree of rulesets and rules
    {
        private MJ_Main main;
        public MJ_Main MJ_Main { get { return main; } }
        private string iType;
        public string IType() { return iType; }
        private string rType;
        public string RType() { return rType; }
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
        private bool repeat; //TODO: Is this necessary? Can this be handled by the ruleset type?
        public bool Repeat () { return repeat; } //Determines behavior for this ruleset when nested inside another one. If true, the ruleset will repeat until its rules are exhausted before continuing with its parent ruleset. If not, it will run once before returning to its parent. This cannot be true for root rulesets.
        private string name;
        public string Name() { return name; }
        private RuleSet parentRuleSet;
        public RuleSet ParentRuleSet() { return parentRuleSet; }
        private List<IRuleSet> childrenRuleSets;
        public List<IRuleSet> ChildRuleSet { get { return childrenRuleSets; } set { childrenRuleSets = value; } }
        public bool IsRoot { get { return parentRuleSet == null; } }
        public RuleSet(string name, List<IRuleSet> children, string ruleSetType, int limit, MJ_Main main) //constructor only for the root ruleset with a specified limit
        {
            this.main = main;
            iType = "RuleSet";
            this.name = name;
            parentRuleSet = null;
            childrenRuleSets = children;
            rType = ruleSetType;
            this.limit = limit;
            uses = 0;
            repeat = false;
        }
        public RuleSet(string name, RuleSet parent, List<IRuleSet> children, string ruleSetType, bool repeat, int limit, MJ_Main main) //constructor for all child rulesets with a specified limit
        {
            this.main = main;
            iType = "RuleSet";
            this.name = name;
            parentRuleSet = parent;
            childrenRuleSets = children;
            rType = ruleSetType;
            this.limit = limit;
            uses = 0;
            this.repeat = repeat;
        }
        private void RecursiveResetUses()
        {
            uses = 0;
            foreach (IRuleSet ir in childrenRuleSets) ir.ResetUses();
        }
        public bool IsFinishedInRound()
        {
            foreach (IRuleSet r in childrenRuleSets)
            {
                if (!r.IsFinishedInRound())
                {
                    return false;
                }
            }
            return true;
        }
        public void ResetFinishedInRound()
        {
            foreach (IRuleSet r in childrenRuleSets)
            {
                r.ResetFinishedInRound();
            }
        }
    }
    public class Rule : IRuleSet //a rule that determines how certain patterns of cells are changed
    {
        private MJ_Main main;
        public MJ_Main MJ_Main { get { return main; } }
        private string iType;
        public string IType() { return iType; }
        private string rType;
        public string RType() { return rType; }
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
        private bool finishedInRound;
        public bool IsFinishedInRound() { return finishedInRound; }
        public void FinishedInRound() { finishedInRound = true; }
        public void ResetFinishedInRound() { finishedInRound = false; }
        private string name;
        public string Name() { return name; }
        private Dictionary<string, string[,,]> strIn;
        public Dictionary<string, string[,,]> StrIn { get { return strIn; } }
        private Dictionary<string, string[,,]> strOut;
        public Dictionary<string, string[,,]> StrOut { get { return strOut; } }
        private bool[] symmetries;
        public bool[] Symmetries;
        private RuleSet parentRuleSet;
        public RuleSet ParentRuleSet() { return parentRuleSet; }
        public Rule(string name, Dictionary<string, string[,,]> strIn, Dictionary<string, string[,,]> strOut, string type, bool[] symmetries, int limit, RuleSet parentRuleSet, MJ_Main mj_main)
        {
            this.main = mj_main;
            iType = "Rule";
            this.name = name;
            this.strIn = strIn;
            this.strOut = strOut;
            this.rType = type;
            this.symmetries = symmetries;
            this.limit = limit;
            uses = 0;
            this.parentRuleSet = parentRuleSet;
            finishedInRound = false;
            GenerateRotationsReflections(strIn, symmetries);
            GenerateRotationsReflections(strOut, symmetries);
        }
        private void GenerateRotationsReflections(Dictionary<string, string[,,]> dic, bool[] sym)
        {
            main.debug.DebugLine("Generating rotations and reflections");
            string[,,] baseRule = dic["base"];
            List<string[,,]> noDupeList = new List<string[,,]>();
            noDupeList.Add(baseRule);
            int xs = baseRule.GetLength(0);
            int ys = baseRule.GetLength(1);
            int zs = baseRule.GetLength(2);
            int xMax = xs - 1;
            int yMax = ys - 1;
            int zMax = zs - 1;
            string[,,] rotx90 = new string[xs, zs, ys];
            string[,,] rotx180 = new string[xs, ys, zs];
            string[,,] rotx270 = new string[xs, zs, ys];
            string[,,] roty90 = new string[zs, ys, xs];
            string[,,] roty180 = new string[xs, ys, zs];
            string[,,] roty270 = new string[zs, ys, xs];
            string[,,] rotz90 = new string[ys, xs, zs];
            string[,,] rotz180 = new string[xs, ys, zs];
            string[,,] rotz270 = new string[ys, xs, zs];
            string[,,] refx = new string[xs, ys, zs];
            string[,,] refy = new string[xs, ys, zs];
            string[,,] refz = new string[xs, ys, zs];
            for (int x = 0; x < baseRule.GetLength(0); x++)
            {
                for (int y = 0; y < baseRule.GetLength(1); y++)
                {
                    for (int z = 0; z < baseRule.GetLength(2); z++)
                    {
                        if (sym[0])
                        {
                            rotx90[x, z, yMax - y] = baseRule[x, y, z];
                            rotx180[x, yMax - y, zMax - z] = baseRule[x, y, z];
                            rotx270[x, zMax - z, y] = baseRule[x, y, z];
                            if (!noDupeList.Contains(rotx90)) { noDupeList.Add(rotx90); }
                            if (!noDupeList.Contains(rotx180)) { noDupeList.Add(rotx180); }
                            if (!noDupeList.Contains(rotx270)) { noDupeList.Add(rotx270); }
                        }
                        if (sym[1])
                        {
                            roty90[z, y, xMax - x] = baseRule[x, y, z];
                            roty180[xMax - x, y, zMax - z] = baseRule[x, y, z];
                            roty270[zMax - z, y, x] = baseRule[x, y, z];
                            if (!noDupeList.Contains(roty90)) { noDupeList.Add(roty90); }
                            if (!noDupeList.Contains(roty180)) { noDupeList.Add(roty180); }
                            if (!noDupeList.Contains(roty270)) { noDupeList.Add(roty270); }
                        }
                        if (sym[2])
                        {
                            rotz90[y, xMax - x, z] = baseRule[x, y, z];
                            rotz180[xMax - x, yMax - y, z] = baseRule[x, y, z];
                            rotz270[yMax - y, x, z] = baseRule[x, y, z];
                            if (!noDupeList.Contains(rotz90)) { noDupeList.Add(rotz90); }
                            if (!noDupeList.Contains(rotz180)) { noDupeList.Add(rotz180); }
                            if (!noDupeList.Contains(rotz270)) { noDupeList.Add(rotz270); }
                        }
                        if (sym[3])
                        {
                            refx[xMax - x, y, z] = baseRule[x, y, z];
                            if (!noDupeList.Contains(refx)) { noDupeList.Add(refx); }
                        }
                        if (sym[4])
                        {
                            refy[x, yMax - y, z] = baseRule[x, y, z];
                            if (!noDupeList.Contains(refy)) { noDupeList.Add(refy); }
                        }
                        if (sym[5])
                        {
                            refz[x, y, zMax - z] = baseRule[x, y, z];
                            if (!noDupeList.Contains(refz)) { noDupeList.Add(refz); }
                        }
                    }
                }
            }
            if (sym[0])
            {
                if (noDupeList.Contains(rotx90))
                {
                    main.debug.DebugLine("Adding rotx90");
                    dic.Add("rotx90", rotx90);
                }
                if (noDupeList.Contains(rotx180))
                {
                    main.debug.DebugLine("Adding rotx180");
                    dic.Add("rotx180", rotx180);
                }
                if (noDupeList.Contains(rotx270))
                {
                    main.debug.DebugLine("Adding rotx270");
                    dic.Add("rotx270", rotx270);
                }
            }
            if (sym[1])
            {
                if (noDupeList.Contains(roty90))
                {
                    main.debug.DebugLine("Adding roty90");
                    dic.Add("roty90", roty90);
                }
                if (noDupeList.Contains(roty180))
                {
                    main.debug.DebugLine("Adding roty180");
                    dic.Add("roty180", roty180);
                }
                if (noDupeList.Contains(roty270))
                {
                    main.debug.DebugLine("Adding roty270");
                    dic.Add("roty270", roty270);
                }
            }
            if (sym[2])
            {
                if (noDupeList.Contains(rotz90))
                {
                    main.debug.DebugLine("Adding rotz90");
                    dic.Add("rotz90", rotz90);
                }
                if (noDupeList.Contains(rotz180))
                {
                    main.debug.DebugLine("Adding rotz180");
                    dic.Add("rotz180", rotz180);
                }
                if (noDupeList.Contains(rotz270))
                {
                    main.debug.DebugLine("Adding rotz270");
                    dic.Add("rotz270", rotz270);
                }
            }
            if (sym[3] && noDupeList.Contains(refx))
            {
                main.debug.DebugLine("Adding refx");
                dic.Add("refx", refx);
            }
            if (sym[4] && noDupeList.Contains(refy))
            {
                main.debug.DebugLine("Adding refy");
                dic.Add("refy", refy);
            }
            if (sym[5] && noDupeList.Contains(refz))
            {
                main.debug.DebugLine("Adding refz");
                dic.Add("refz", refz);
            }
        }
    }
    public struct RuleMatch //conveys the necessary information to apply a matched rule
    {
        public bool MatchFound { get; }
        public Cell[,,] Region { get; }
        public Rule Rule { get; }
        public string TransformKey { get; }
        public RuleMatch(Cell[,,] region, Rule rule, string transformKey) //use this constructor when a match is found
        {
            Region = region;
            Rule = rule;
            TransformKey = transformKey;
            MatchFound = true;
        }
        public RuleMatch(Rule rule) //use this constructor when a match is not found
        {
            Region = null;
            this.Rule = rule;
            TransformKey = null;
            MatchFound = false;
        }
    }
    public class Cell //one cell in a space
    {
        private int x, y, z; //coordinates of this cell in MJ_Space
        public int X { get { return x; } }
        public int Y { get { return y; } }
        public int Z { get { return z; } }
        private string state; //do not modify directly; use SetCellState()
        public string State { get { return state; } }
        private Space space;
        public Cell(int x, int y, int z, string state, Space space)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.state = state;
            this.space = space;
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
                debugText.Add("EXCEPTION: " + line);
                throw new Exception("EXCEPTION: " + line);
            }
        }
    }
    public static class Shuffle //contains some methods to shuffle different collections
    {
        public static List<T> RandomList<T>(List<T> input, Random rng)
        {
            T[] array = new T[input.Count];
            List<int> remainingIndicies = new List<int>();
            for (int a = 0; a < input.Count; a++)
            {
                remainingIndicies.Add(a);
            }
            int i = 0;
            while (remainingIndicies.Count > 0)
            {
                int r = remainingIndicies[rng.Next(remainingIndicies.Count)];
                array[i] = input[r];
                remainingIndicies.Remove(r);
                i++;
            }
            return new List<T>(array);
        }
        public static Queue<T> RandomQueue<T>(List<T> input, Random rng) 
        {
            return new Queue<T>(RandomList(input, rng));
        }
        public static Queue<T> RandomQueue<T>(T[,,] input, Random rng)
        {
            List<T> list = new List<T>();
            foreach(T t in input)
            {
                list.Add(t);
            }
            List<T> randomList = RandomList(list, rng);
            return new Queue<T>(randomList);
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
                        if (ruleNode.Attributes["type"].Value != null && ValidRType("ruleset", ruleNode.Attributes["type"].Value))
                        {
                            string type = ruleNode.Attributes["type"].Value;
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
            string type = "single";
            if (node.Attributes["type"].Value != null && ValidRType("rule", node.Attributes["type"].Value))
            {
                type = node.Attributes["type"].Value;
            }
            Rule r = new Rule(node.Attributes["name"].Value, stringTo3DStringArrayDic(ruleIn, sym, main), stringTo3DStringArrayDic(ruleOut, sym, main), type, sym, limit, parent, main);
            return r;
        }
        private static bool ValidRType(string key, string value)
        {
            Dictionary<string, List<string>> dict= new Dictionary<string, List<string>>();
            List<string> ruleTypes = new List<string>();
            ruleTypes.Add("single");
            ruleTypes.Add("parallel");
            List<string> ruleSetTypes = new List<string>();
            ruleSetTypes.Add("series");
            ruleSetTypes.Add("sequence");
            ruleSetTypes.Add("retrace");
            ruleSetTypes.Add("random");
            dict.Add("rule", ruleTypes);
            dict.Add("ruleset", ruleSetTypes);
            if (dict[key].Contains(value))
            {
                return true;
            }
            throw new Exception("INVALID RULE OR RULESET TYPE");
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