using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sim
{
    class Pacman : Actor
    {
        public Pacman(string filename)
        {
            using (var file = new StreamReader(filename))
            {
                this.code = Program.TokenizeStream(file, ' ')
                    .Select(i => new PacmanInstruction(i))
                    .ToList();
            }

            this.Lives = 3;
            this.ScheduledTick = 127;
        }

        public void CallMain()
        {
            var ans = ExecuteAi(this.code, 0, new PacmanEnvironmentFrame(EncodeCurrentState(), null), 0);
            this.currentState = ans.Item1;
            this.stepFunction = ans.Item2;
        }

        protected override int TickImpl()
        {
            var ans = ExecuteAi(this.code, this.stepFunction.IntValue, new PacmanEnvironmentFrame(this.currentState, EncodeCurrentState()), this.ScheduledTick);
            if (!Enum.IsDefined(typeof(Direction), ans.Item2.IntValue))
            {
                throw new Exception(); // TODO
            }
            this.currentState = ans.Item1;
            this.Direction = (Direction)ans.Item2.IntValue;

            var newPosition = this.CurrentPosition.Move(this.Direction);
            if (this.Map[newPosition] != MapCell.Wall)
            {
                this.CurrentPosition = newPosition;
                Program.LogMove(this.ScheduledTick, @"\\", this.CurrentPosition);
            }

            return foods.Contains(this.Map[this.CurrentPosition]) ? 137 : 127;
        }

        public int Lives { get; set; }
        private static PacmanValue ExecuteAi(
            List<PacmanInstruction> code,
            int pc,
            PacmanEnvironmentFrame initialEnvironmentFrame,
            int tick)
        {
            const bool PROFILE = false;

            var iter = 0;
            var dataStack = new List<PacmanValue>();
            var controlStack = new List<PacmanValue>() { new PacmanValue(PacmanTag.Stop) };
            var currentEnvironmentFrame = initialEnvironmentFrame;
            var profileData = new Dictionary<string, int>();
            var funcCallData = new Dictionary<int, int>();

            while (true)
            {
                if (PROFILE)
                {
                    var profileKey = string.Join(", ", controlStack
                        .Where(i => i.Tag == PacmanTag.Ret)
                        .Select(i => i.ProfileAddress.ToString())
                        .LastOrDefault());
                    int profileValue;
                    profileData.TryGetValue(profileKey, out profileValue);
                    profileData[profileKey] = profileValue + 1;
                }

                ++iter;
                PacmanValue xx, yy;
                PacmanEnvironmentFrame frame, newFrame;
                PacmanInstruction inst = code[pc];
                int funcCallDataValue;

                switch (inst.Opcode)
                {
                    case PacmanOpcode.Ldc:
                        dataStack.Add(PacmanValue.Int(inst.Operands[0]));
                        ++pc;
                        break;
                    case PacmanOpcode.Ld:
                        frame = GetEnvironmentFrame(currentEnvironmentFrame, inst.Operands[0]);
                        dataStack.Add(frame.Values[inst.Operands[1]]);
                        ++pc;
                        break;
                    case PacmanOpcode.Add:
                        DoMath(dataStack, (x, y) => x + y);
                        ++pc;
                        break;
                    case PacmanOpcode.Sub:
                        DoMath(dataStack, (x, y) => x - y);
                        ++pc;
                        break;
                    case PacmanOpcode.Mul:
                        DoMath(dataStack, (x, y) => x * y);
                        ++pc;
                        break;
                    case PacmanOpcode.Div:
                        DoMath(dataStack, (x, y) => (int)Math.Floor((double)x / (double)y));
                        ++pc;
                        break;
                    case PacmanOpcode.Ceq:
                        DoMath(dataStack, (x, y) => x == y ? 1 : 0);
                        ++pc;
                        break;
                    case PacmanOpcode.Cgt:
                        DoMath(dataStack, (x, y) => x > y ? 1 : 0);
                        ++pc;
                        break;
                    case PacmanOpcode.Cgte:
                        DoMath(dataStack, (x, y) => x >= y ? 1 : 0);
                        ++pc;
                        break;
                    case PacmanOpcode.Atom:
                        xx = dataStack.Pop();
                        dataStack.Add(PacmanValue.Int(xx.Tag == PacmanTag.Int ? 1 : 0));
                        ++pc;
                        break;
                    case PacmanOpcode.Cons:
                        yy = dataStack.Pop();
                        xx = dataStack.Pop();
                        dataStack.Add(PacmanValue.Cons(xx, yy));
                        ++pc;
                        break;
                    case PacmanOpcode.Car:
                        xx = dataStack.Pop();
                        xx.VerifyTag(PacmanTag.Cons);
                        dataStack.Add(xx.Item1);
                        ++pc;
                        break;
                    case PacmanOpcode.Cdr:
                        xx = dataStack.Pop();
                        xx.VerifyTag(PacmanTag.Cons);
                        dataStack.Add(xx.Item2);
                        ++pc;
                        break;
                    case PacmanOpcode.Sel:
                    case PacmanOpcode.Tsel:
                        xx = dataStack.Pop();
                        xx.VerifyTag(PacmanTag.Int);
                        if (inst.Opcode == PacmanOpcode.Sel)
                        {
                            controlStack.Add(PacmanValue.Join(pc + 1));
                        }
                        pc = xx.IntValue == 0 ? inst.Operands[1] : inst.Operands[0];
                        break;
                    case PacmanOpcode.Join:
                        xx = controlStack.Pop();
                        xx.VerifyTag(PacmanTag.Join);
                        pc = xx.IntValue;
                        break;
                    case PacmanOpcode.Ldf:
                        dataStack.Add(PacmanValue.Closure(inst.Operands[0], currentEnvironmentFrame));
                        ++pc;
                        break;
                    case PacmanOpcode.Tap:
                    case PacmanOpcode.Ap:
                        xx = dataStack.Pop();
                        xx.VerifyTag(PacmanTag.Closure);

                        newFrame = new PacmanEnvironmentFrame()
                        {
                            Parent = currentEnvironmentFrame,
                            IsValid = true,
                            Values = new List<PacmanValue>()
                        };

                        for (var i = 0; i < inst.Operands[0]; ++i)
                        {
                            newFrame.Values.Add(dataStack.Pop());
                        }
                        newFrame.Values.Reverse();

                        if (inst.Opcode == PacmanOpcode.Ap)
                        {
                            controlStack.Add(PacmanValue.Ret(pc + 1, currentEnvironmentFrame, xx.IntValue));
                            if (PROFILE)
                            {
                                funcCallData.TryGetValue(xx.IntValue, out funcCallDataValue);
                                funcCallData[xx.IntValue] = funcCallDataValue + 1;
                            }
                        }

                        currentEnvironmentFrame = newFrame;
                        pc = xx.IntValue;
                        break;
                    case PacmanOpcode.Rtn:
                        xx = controlStack.Pop();
                        if (xx.Tag == PacmanTag.Stop)
                        {
                            Program.LogDebug(tick, "iters = {0}", iter);
                            if (PROFILE)
                            {
                                Program.LogProfileInfo(tick, profileData, funcCallData);
                            }
                            return dataStack.Pop();
                        }
                        xx.VerifyTag(PacmanTag.Ret);
                        pc = xx.IntValue;
                        currentEnvironmentFrame = xx.EnvironmentFrameValue;
                        break;
                    case PacmanOpcode.Dum:
                        newFrame = new PacmanEnvironmentFrame()
                        {
                            Parent = currentEnvironmentFrame,
                            IsValid = false,
                            Values = new List<PacmanValue>()
                        };

                        for (var i = 0; i < inst.Operands[0]; ++i)
                        {
                            newFrame.Values.Add(null);
                        }

                        currentEnvironmentFrame = newFrame;
                        ++pc;
                        break;
                    case PacmanOpcode.Rap:
                    case PacmanOpcode.Trap:
                        xx = dataStack.Pop();
                        xx.VerifyTag(PacmanTag.Closure);
                        newFrame = xx.EnvironmentFrameValue;
                        if (newFrame.IsValid ||
                            newFrame.Values.Count != inst.Operands[0] ||
                            currentEnvironmentFrame != newFrame)
                        {
                            throw new Exception(); // TODO
                        }

                        newFrame.Values.Clear();
                        for (var i = 0; i < inst.Operands[0]; ++i)
                        {
                            newFrame.Values.Add(dataStack.Pop());
                        }
                        newFrame.Values.Reverse();

                        if (inst.Opcode == PacmanOpcode.Rap)
                        {
                            controlStack.Add(PacmanValue.Ret(pc + 1, currentEnvironmentFrame.Parent, xx.IntValue));

                            if (PROFILE)
                            {
                                funcCallData.TryGetValue(xx.IntValue, out funcCallDataValue);
                                funcCallData[xx.IntValue] = funcCallDataValue + 1;
                            }
                        }

                        newFrame.IsValid = true;
                        currentEnvironmentFrame = newFrame;
                        pc = xx.IntValue;
                        break;
                    case PacmanOpcode.Stop:
                        return dataStack.Pop();
                    case PacmanOpcode.St:
                        frame = GetEnvironmentFrame(currentEnvironmentFrame, inst.Operands[0]);
                        xx = dataStack.Pop();
                        frame.Values[inst.Operands[1]] = xx;
                        ++pc;
                        break;
                    case PacmanOpcode.Dbug:
                        xx = dataStack.Pop();
                        Program.LogDebug(tick, "DBUG: '{0}'", xx);
                        ++pc;
                        break;
                    case PacmanOpcode.Brk:
                        ++pc;
                        break;
                    default:
                        throw new Exception(); // TODO
                }
            }
        }

        private static void DoMath(List<PacmanValue> dataStack, Func<int, int, int> fn)
        {
            var y = dataStack.Pop();
            var x = dataStack.Pop();
            x.VerifyTag(PacmanTag.Int);
            y.VerifyTag(PacmanTag.Int);
            dataStack.Add(PacmanValue.Int(fn(x.IntValue, y.IntValue)));
        }

        private static PacmanEnvironmentFrame GetEnvironmentFrame(
            PacmanEnvironmentFrame currentEnvironmentFrame,
            int n)
        {
            var ans = currentEnvironmentFrame;
            for (var i = 0; i < n; ++i)
            {
                ans = ans.Parent;
            }

            if (!ans.IsValid)
            {
                throw new Exception(); // TODO
            }

            return ans;
        }

        private PacmanValue EncodeCurrentState()
        {
            return PacmanValue.Tuple(
                EncodeMap(),
                PacmanValue.Tuple(
                    PacmanValue.Int(Math.Max(0, this.Map.FrightModeDeactivateTime - this.ScheduledTick)),
                    PacmanValue.Point(this.CurrentPosition),
                    PacmanValue.Int((int)this.Direction),
                    PacmanValue.Int(this.Lives),
                    PacmanValue.Int(this.Map.Points)),
                PacmanValue.List(this.Map.Ghosts.Select(i => 
                    PacmanValue.Tuple(
                        PacmanValue.Int((int)i.Vitality),
                        PacmanValue.Point(i.CurrentPosition),
                        PacmanValue.Int((int)i.Direction)))),
                PacmanValue.Int(Math.Max(0, this.Map.FruitDeactivateTime - this.ScheduledTick)));
        }

        private PacmanValue EncodeMap()
        {
            var ans = new List<PacmanValue>();

            for (var y = 0; y < this.Map.Height; ++y)
            {
                var row = new List<PacmanValue>();
                for (var x = 0; x < this.Map.Width; ++x)
                {
                    row.Add(PacmanValue.Int((int)this.Map[x, y]));
                }
                ans.Add(PacmanValue.List(row));
            }

            return PacmanValue.List(ans);
        }

        private static readonly MapCell[] foods = new MapCell[] { MapCell.Pill, MapCell.PowerPill, MapCell.Fruit };

        private List<PacmanInstruction> code;
        private PacmanValue currentState;
        private PacmanValue stepFunction;
    }


    enum PacmanOpcode
    {
        Ldc,
        Ld,
        Add,
        Sub,
        Mul,
        Div,
        Ceq,
        Cgt,
        Cgte,
        Atom,
        Cons,
        Car,
        Cdr,
        Sel,
        Join,
        Ldf,
        Ap,
        Rtn,
        Dum,
        Rap,
        Stop,
        Tsel,
        Tap,
        Trap,
        St,
        Dbug,
        Brk,
    }


    enum PacmanTag
    {
        Int, 
        Cons,
        Join,
        Closure,
        Ret,
        Stop,
    }


    class PacmanEnvironmentFrame
    {
        public PacmanEnvironmentFrame()
        {
        }

        public PacmanEnvironmentFrame(
            PacmanValue arg1,
            PacmanValue arg2)
        {
            Parent = null;
            IsValid = true;
            Values = new List<PacmanValue>() { arg1, arg2 };
        }

        public PacmanEnvironmentFrame Parent { get; set; }
        public bool IsValid { get; set; }
        public List<PacmanValue> Values { get; set; }

        public override string ToString()
        {
            int depth = 0;
            PacmanEnvironmentFrame cur = this;
            while (cur != null)
            {
                cur = cur.Parent;
                ++depth;
            }
            return string.Format("env:({0},{1})", depth, Values.Count);
        }
    }


    class PacmanValue
    {
        public PacmanValue(PacmanTag tag)
        {
            this.Tag = tag;
        }

        public PacmanTag Tag { get; set; }
        public int IntValue { get; set; }
        public int ProfileAddress { get; set; }
        public PacmanValue Item1 { get; set; }
        public PacmanValue Item2 { get; set; }
        public PacmanEnvironmentFrame EnvironmentFrameValue { get; set; }

        public override string ToString()
        {
            switch(this.Tag)
            {
                case PacmanTag.Int:
                    return string.Format("{0}", this.IntValue);
                case PacmanTag.Closure:
                    return string.Format("closure:({0},{1})", this.IntValue, this.EnvironmentFrameValue);
                case PacmanTag.Cons:
                    return string.Format("({0},{1})", this.Item1, this.Item2);
                case PacmanTag.Join:
                    return string.Format("join:({0})", this.IntValue);
                case PacmanTag.Ret:
                    return string.Format("ret:({0},{1},{2})", this.IntValue, this.EnvironmentFrameValue, this.ProfileAddress);
                case PacmanTag.Stop:
                    return string.Format("stop");
                default:
                    throw new Exception();
            }
        }

        public void VerifyTag(PacmanTag tag)
        {
            if (tag != this.Tag)
            {
                throw new Exception(); // TODO
            }
        }

        public static PacmanValue Int(int x)
        {
            return new PacmanValue(PacmanTag.Int) { IntValue = x };
        }

        public static PacmanValue Join(int x)
        {
            return new PacmanValue(PacmanTag.Join) { IntValue = x };
        }

        public static PacmanValue Cons(PacmanValue item1, PacmanValue item2)
        {
            return new PacmanValue(PacmanTag.Cons) { Item1 = item1, Item2 = item2 };
        }

        public static PacmanValue Closure(int address, PacmanEnvironmentFrame environmentFrame)
        {
            return new PacmanValue(PacmanTag.Closure) { IntValue = address, EnvironmentFrameValue = environmentFrame };
        }

        public static PacmanValue Ret(int address, PacmanEnvironmentFrame environmentFrame, int profileAddress)
        {
            return new PacmanValue(PacmanTag.Ret) { IntValue = address, EnvironmentFrameValue = environmentFrame, ProfileAddress = profileAddress };
        }

        public static PacmanValue Point(Point pt)
        {
            return PacmanValue.Cons(PacmanValue.Int(pt.x), PacmanValue.Int(pt.y));
        }

        public static PacmanValue Tuple(params PacmanValue[] args)
        {
            var ans = args.Last();
            foreach (var arg in args.Reverse().Skip(1))
            {
                ans = PacmanValue.Cons(arg, ans);
            }
            return ans;
        }

        public static PacmanValue List(IEnumerable<PacmanValue> args)
        {
            return PacmanValue.Tuple(args.Concat(Zero).ToArray());
        }

        private static readonly List<PacmanValue> Zero = new List<PacmanValue>() { PacmanValue.Int(0) };
    }


    class PacmanInstruction
    {
        public PacmanInstruction(List<string> tokens)
        {
            this.Opcode = (PacmanOpcode)Enum.Parse(typeof(PacmanOpcode), tokens[0], true);
            this.Operands = tokens.Skip(1).Select(i => int.Parse(i)).ToList();
        }

        public PacmanOpcode Opcode { get; set; }
        public List<int> Operands { get; set; }
    }
}
