using System;
using Ilmn.Das.App.Wittyer.Utilities;
using Ilmn.Das.Std.AppUtils.Collections;
using Ilmn.Das.Std.AppUtils.Misc;

namespace Ilmn.Das.App.Wittyer.Vcf.Variants
{
    public class BorderDistance : IComparable<BorderDistance>
    {
        private BorderDistance(uint lbl, uint lbr, uint rbl, uint rbr)
        {
            LeftBorderLeft = lbl;
            LeftBorderRight = lbr;
            RightBorderLeft = rbl;
            RightBorderRight = rbr;
        }

        public uint LeftBorderLeft { get; }

        public uint LeftBorderRight { get; }

        public uint RightBorderLeft { get; }

        public uint RightBorderRight { get; }

        public uint Score => LeftBorderLeft + LeftBorderRight + RightBorderLeft + RightBorderRight;

        public int CompareTo(BorderDistance other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Score.CompareTo(other.Score);
        }

        public static BorderDistance Create(uint lbl, uint lbr, uint rbl, uint rbr)
        {
            return new BorderDistance(lbl, lbr, rbl, rbr);
        }

        public override string ToString()
        {
            return LeftBorderLeft.FollowedBy(LeftBorderRight, RightBorderLeft, RightBorderRight)
                .StringJoin(WittyerConstants.BorderDistanceDelimiter);
        }

        public static BorderDistance CreateFromVariant(IWittyerSimpleVariant first, IWittyerSimpleVariant second)
        {
            var firstCiPos = first.OriginalVariant.ParseCi(WittyerConstants.Cipos);
            var secondCiPos = second.OriginalVariant.ParseCi(WittyerConstants.Cipos);
            var lbl = GetDistance((uint) (first.OriginalVariant.Position + firstCiPos.Start),
                (uint) (second.OriginalVariant.Position + secondCiPos.Start));
            var lbr = GetDistance((uint) (first.OriginalVariant.Position + firstCiPos.Stop),
                (uint) (second.OriginalVariant.Position + secondCiPos.Stop));

            var rbl = GetDistance(first.Stop, second.Stop);
            var rbr = rbl;

            if (first is IWittyerVariant normalFirst && second is IWittyerVariant normalSecond)
            {
                var firstCiend = normalFirst.OriginalVariant.ParseCi(WittyerConstants.Ciend);
                var secondCiend = normalSecond.OriginalVariant.ParseCi(WittyerConstants.Ciend);

                rbl = GetDistance((uint) (normalFirst.Stop + firstCiend.Start),
                    (uint) (normalSecond.Stop + secondCiend.Start));
                rbr = GetDistance((uint) (normalFirst.Stop + firstCiend.Stop),
                    (uint) (normalSecond.Stop + secondCiend.Stop));
            }else if (first is IWittyerBnd bndFirst && second is IWittyerBnd bndSecond)
            {
                var firstCipos = bndFirst.EndOriginalVariant.ParseCi(WittyerConstants.Cipos);
                var secondCipos = bndSecond.EndOriginalVariant.ParseCi(WittyerConstants.Cipos);

                rbl = GetDistance((uint) (bndFirst.EndOriginalVariant.Position + firstCipos.Start),
                    (uint) (bndSecond.EndOriginalVariant.Position + secondCipos.Start));
                rbr = GetDistance((uint) (bndFirst.EndOriginalVariant.Position + firstCipos.Stop),
                    (uint) (bndSecond.EndOriginalVariant.Position + secondCipos.Stop));
            }

                return Create(lbl, lbr, rbl, rbr);
        }

        internal static uint GetDistance(uint big, uint small)
        {
            return small > big ? small - big : big - small;
        }
    }
}