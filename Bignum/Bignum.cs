﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Bignum {
    public struct Bignum {
        // Tail stores the sign in its highest bit. The other bits indicate
        // the absolute value of the number. Head extends the value in tail,
        // with the elements in order of decreasing significance.
        readonly uint tail;
        readonly uint[] head;

        public Bignum(int value) {
            if (value == Int32.MinValue) {
                this.head = new uint[] { 0x1 };
                this.tail = 0x80000000;
                return;
            }

            uint bottom = (uint) (Math.Abs(value) & 0x7FFFFFFF);
            if (value < 0)
                bottom = bottom | 0x80000000;

            this.head = null;
            this.tail = bottom;
        }

        public Bignum(uint value) {
            uint top = (value & 0x80000000) == 0 ? 0 : (uint)1;
            uint bottom = (value & 0x7FFFFFFF);

            this.head = top > 0 ? new[] { top } : null;
            this.tail = bottom;
        }

        public Bignum(long value) {
            // Abs doesn't work on Int64.MinValue, so we have to use a workaround.
            if (value == Int64.MinValue) {
                this.head = new uint[] { 0x1, 0x0 };
                this.tail = 0x80000000;
                return;
            }

            uint bottom = (uint)(Math.Abs(value) & 0x7FFFFFFF);
            if (value < 0)
                bottom = (((uint) bottom) | 0x80000000);

            uint top = (uint)((Math.Abs(value) >> 31) & 0xFFFFFFFF);

            this.head = top > 0 ? new[] { top } : null;
            this.tail = bottom;
        }

        public Bignum(ulong value) {
            uint bottom = (uint) (value & 0x7FFFFFFF);
            value = value >> 31;
            uint chunk1 = (uint)(value & 0xFFFFFFFF);
            uint chunk2 = (value & 0x100000000) == 0 ? 0 : (uint)1;

            if (chunk1 == 0 && chunk2 == 0)
                this.head = null;
            else if (chunk2 == 0)
                this.head = new[] { chunk1 };
            else
                this.head = new[] { chunk2, chunk1 };
            this.tail = bottom;
        }

        public int Sign {
            get {
                if (tail == 0 && (head == null || head.Length == 0))
                    return 0;

                return (tail & 0x80000000) == 0 ? 1 : -1;               
            }
        }

        #region Conversion Operators

        public static implicit operator Bignum(int value) {
            return new Bignum(value);
        }

        public static implicit operator Bignum(uint value) {
            return new Bignum(value);
        }

        public static implicit operator Bignum(long value) {
            return new Bignum(value);
        }

        public static implicit operator Bignum(ulong value) {
            return new Bignum(value);
        }

        public static explicit operator int(Bignum bignum) {
            int value = (int) (bignum.tail & 0x7FFFFFFF);

            if (bignum.head != null) {
                if (bignum.head.Length > 1)
                    throw new OverflowException(); // Value too big.
                else if (bignum.head.Length == 1) {
                    // The only allowed value here is Int32.MinValue
                    if (bignum.head[0] == 1 && bignum.tail == 0x80000000)
                        return Int32.MinValue;
                    else
                        throw new OverflowException();
                }
            }

            return value * bignum.Sign;
        }

        public static explicit operator uint(Bignum bignum) {
            if (bignum.Sign == -1)
                throw new OverflowException(); // Can't cast negative to a uint.

            uint value = (uint)bignum.tail;
            if (bignum.head != null) {
                if (bignum.head.Length > 1 || bignum.head[0] > 1)
                    throw new OverflowException(); // Value too big.

                value += bignum.head[0] << 31;
            }

            return value;
        }

        public static explicit operator long(Bignum bignum) {
            long value = bignum.tail & 0x7FFFFFFF;

            if (bignum.head != null) {
                if (bignum.head.Length > 2)
                    throw new OverflowException(); // Value too big.
                else if (bignum.head.Length == 1)
                    value += ((long)bignum.head[0]) << 31;
                else if (bignum.head.Length == 2) {
                    // The only allowed value here is Int64.MinValue
                    if (bignum.head[0] == 1 && bignum.head[1] == 0 && bignum.tail == 0x80000000)
                        return Int64.MinValue;
                    else
                        throw new OverflowException();
                }
            }

            return value * bignum.Sign;
        }

        public static explicit operator ulong(Bignum bignum) {
            if (bignum.Sign == -1)
                throw new OverflowException(); // Can't cast negative to a ulong.

            ulong value = (ulong)bignum.tail;
            if (bignum.head != null) {
                if (bignum.head.Length > 2)
                    throw new OverflowException(); // Value too big.
                else if (bignum.head.Length == 1)
                    value += ((ulong)bignum.head[0]) << 31;
                else if (bignum.head.Length == 2) {
                    if (bignum.head[0] > 1)
                        throw new OverflowException(); // Value too big.

                    value += ((ulong)bignum.head[0]) << 63;
                    value += ((ulong)bignum.head[1]) << 31;
                }
            }

            return value;
        }

        #endregion

        #region Equals and GetHashCode

        public static bool operator ==(Bignum a, Bignum b) {
            return a.Equals(b);
        }

        public static bool operator !=(Bignum a, Bignum b) {
            return !a.Equals(b);
        }

        public static bool Equals(Bignum a, Bignum b) {
            if (a.tail != b.tail)
                return false;

            if (a.head == null ^ b.head == null)
                return false;

            if (a.head != null) {
                if (a.head.Length != b.head.Length)
                    return false;

                for (int i = 0; i < a.head.Length; i++) {
                    if (a.head[i] != b.head[i])
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) {
            if (!(obj is Bignum))
                return false;

            var bignum = (Bignum)obj;
            return Equals(this, bignum);
        }

        public override int GetHashCode() {
            // Based on http://stackoverflow.com/a/263416
            int value = 17;
            value = value * 23 + (int)(tail & 0x7FFFFFFF);
            if(head != null)
                foreach (var item in head) {
                    value = value * 23 + (int) (item & 0x7FFFFFFF);
                }

            return value;
        }

        #endregion
    }
}
