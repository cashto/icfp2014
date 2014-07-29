using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sim
{
    class Ghost : Actor
    {
        public Ghost(string filename, byte ghostIndex)
        {
            using (var file = new StreamReader(filename))
            {
                this.code = Program.TokenizeStream(file, ',')
                    .Select(i => new GhostInstruction(i))
                    .ToList();
            }

            this.GhostIndex = ghostIndex;
            this.Vitality = GhostVitality.Standard;
            this.ScheduledTick = standardTicks[this.GhostIndex % 4];
        }

        public byte GhostIndex { get; private set; }
        public GhostVitality Vitality { get; set; }

        protected override int TickImpl()
        {
            // TODO: catch exceptions.
            var oldDirection = this.Direction;
            this.requestedDirection = this.Direction;
            ExecuteAi();
            
            this.Direction = ComputeDirection(this.requestedDirection, oldDirection);
            this.CurrentPosition = this.CurrentPosition.Move(this.Direction);
            Program.LogMove(this.ScheduledTick, string.Format("={0}", this.GhostIndex), this.CurrentPosition);

            return this.Vitality == GhostVitality.Fright ? frightTicks[this.GhostIndex % 4] : standardTicks[this.GhostIndex % 4];
        }


        private Direction ComputeDirection(
            Direction requestedDirection,
            Direction oldDirection)
        {
            var directions = new List<Direction>() { requestedDirection, oldDirection, Direction.Up, Direction.Right, Direction.Down, Direction.Left };
            var oppositeDirection = Program.OppositeDirection(oldDirection);

            foreach (var direction in directions)
            {
                if (direction != oppositeDirection &&
                    this.Map[this.CurrentPosition.Move(direction)] != MapCell.Wall)
                {
                    return direction;
                }
            }

            return oppositeDirection;
        }


        private void ExecuteAi()
        {
            this.pc = 0;
            for (var i = 0; i < 1024; ++i)
            {
                byte oldPc = this.pc;
                GhostInstruction inst = this.code[this.pc];
                switch (inst.Opcode)
                {
                    case GhostOpcode.Mov:
                        Set(inst.Operands[0], Get(inst.Operands[1]), true);
                        break;
                    case GhostOpcode.Inc:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) + 1));
                        break;
                    case GhostOpcode.Dec:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) - 1));
                        break;
                    case GhostOpcode.Add:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) + Get(inst.Operands[1])));
                        break;
                    case GhostOpcode.Sub:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) - Get(inst.Operands[1])));
                        break;
                    case GhostOpcode.Mul:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) * Get(inst.Operands[1])));
                        break;
                    case GhostOpcode.Div:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) / Get(inst.Operands[1])));
                        break;
                    case GhostOpcode.And:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) & Get(inst.Operands[1])));
                        break;
                    case GhostOpcode.Or:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) | Get(inst.Operands[1])));
                        break;
                    case GhostOpcode.Xor:
                        Set(inst.Operands[0], (byte)(Get(inst.Operands[0]) ^ Get(inst.Operands[1])));
                        break;
                    case GhostOpcode.Jlt:
                        if (Get(inst.Operands[1]) < Get(inst.Operands[2]))
                        {
                            pc = Get(inst.Operands[0]);
                        }
                        break;
                    case GhostOpcode.Jeq:
                        if (Get(inst.Operands[1]) == Get(inst.Operands[2]))
                        {
                            pc = Get(inst.Operands[0]);
                        }
                        break;
                    case GhostOpcode.Jgt:
                        if (Get(inst.Operands[1]) > Get(inst.Operands[2]))
                        {
                            pc = Get(inst.Operands[0]);
                        }
                        break;
                    case GhostOpcode.Int:
                        switch (Get(inst.Operands[0]))
                        {
                            case 0:
                                int direction = this.registers[0];
                                if (Enum.IsDefined(typeof(Direction), direction))
                                {
                                    this.requestedDirection = (Direction)direction;
                                }
                                break;
                            case 1:
                                this.registers[0] = this.Map.Pacman.CurrentPosition.x;
                                this.registers[1] = this.Map.Pacman.CurrentPosition.y;
                                break;
                            case 2:
                                throw new NotImplementedException();
                            case 3:
                                this.registers[0] = this.GhostIndex;
                                break;
                            case 4:
                                var idx = this.registers[0];
                                if (idx < this.Map.Ghosts.Count)
                                {
                                    this.registers[0] = this.Map.Ghosts[idx].StartingPosition.x;
                                    this.registers[1] = this.Map.Ghosts[idx].StartingPosition.y;
                                }
                                break;
                            case 5:
                                idx = this.registers[0];
                                if (idx < this.Map.Ghosts.Count)
                                {
                                    this.registers[0] = this.Map.Ghosts[idx].CurrentPosition.x;
                                    this.registers[1] = this.Map.Ghosts[idx].CurrentPosition.y;
                                }
                                break;
                            case 6:
                                idx = this.registers[0];
                                if (idx < this.Map.Ghosts.Count)
                                {
                                    this.registers[0] = (byte)this.Map.Ghosts[idx].Vitality;
                                    this.registers[1] = (byte)this.Map.Ghosts[idx].Direction;
                                }
                                break;
                            case 7:
                                var x = this.registers[0];
                                var y = this.registers[1];
                                this.registers[0] = (byte)this.Map[x, y];
                                break;
                            case 8:
                                Program.LogDebug(this.ScheduledTick, "INT8: pc:{0} regs:({1}) mem:({2})",
                                    pc,
                                    string.Join(", ", this.registers.Select(ii => ii.ToString())),
                                    string.Join(", ", this.data.Take(8).Select(ii => ii.ToString())));
                                break;
                        }
                        break;
                    case GhostOpcode.Hlt:
                        return;
                }

                if (oldPc == this.pc)
                {
                    ++this.pc;
                }
            }
        }

        private byte Get(GhostOperand operand)
        {
            switch (operand.Type)
            {
                case GhostOperandType.ProgramCounter:
                    return pc;
                case GhostOperandType.Constant:
                    return operand.Value;
                case GhostOperandType.ConstantIndirect:
                    return this.data[operand.Value];
                case GhostOperandType.Register:
                    return this.registers[operand.Value];
                case GhostOperandType.RegisterIndirect:
                    return this.data[this.registers[operand.Value]];
                default:
                    throw new Exception();
            }
        }

        private void Set(GhostOperand operand, byte value, bool allowPc = false)
        {
            switch (operand.Type)
            {
                case GhostOperandType.ConstantIndirect:
                    this.data[operand.Value] = value;
                    break;
                case GhostOperandType.Register:
                    this.registers[operand.Value] = value;
                    break;
                case GhostOperandType.RegisterIndirect:
                    this.data[this.registers[operand.Value]] = value;
                    break;
                default:
                    if (allowPc && operand.Type == GhostOperandType.ProgramCounter)
                    {
                        pc = value;
                        break;
                    }
                    throw new Exception();
            }
        }

        private static readonly int[] frightTicks = new int[] { 195, 198, 201, 204 };
        private static readonly int[] standardTicks = new int[] { 130, 132, 134, 136 };

        private byte pc;
        private byte[] data = new byte[256];
        private byte[] registers = new byte[8];
        private List<GhostInstruction> code;
        private Direction requestedDirection;
    }


    class GhostInstruction
    {
        public GhostInstruction(List<string> tokens)
        {
            this.Opcode = (GhostOpcode)Enum.Parse(typeof(GhostOpcode), tokens[0], true);
            this.Operands = tokens.Skip(1).Select(i => new GhostOperand(i)).ToList();
        }

        public GhostOpcode Opcode { get; private set; }
        public List<GhostOperand> Operands { get; private set; }
    }


    enum GhostOpcode
    {
        Mov,
        Inc,
        Dec,
        Add,
        Sub,
        Mul,
        Div,
        And,
        Or,
        Xor,
        Jlt,
        Jeq,
        Jgt,
        Int,
        Hlt
    }


    enum GhostOperandType
    {
        ProgramCounter,
        Register,
        RegisterIndirect,
        Constant,
        ConstantIndirect
    }


    enum GhostVitality
    {
        Standard = 0,
        Fright = 1,
        Invisible = 2
    }

    
    class GhostOperand
    {
        public GhostOperand(string s)
        {
            bool isIndirect = s.StartsWith("[") && s.EndsWith("]");
            if (isIndirect)
            {
                s = s.Substring(1, s.Length - 2);
            }

            byte value;
            if (byte.TryParse(s, out value))
            {
                this.Type = isIndirect ? GhostOperandType.ConstantIndirect : GhostOperandType.Constant;
                this.Value = value;
            }
            else if (s.ToUpper() == "PC")
            {
                this.Type = GhostOperandType.ProgramCounter;
            }
            else
            {
                this.Type = isIndirect ? GhostOperandType.RegisterIndirect : GhostOperandType.Register;
                this.Value = (byte)(s.ToUpper()[0] - 'A');
            }
        }

        public GhostOperandType Type { get; private set; }
        public byte Value { get; private set; }
    }

}
