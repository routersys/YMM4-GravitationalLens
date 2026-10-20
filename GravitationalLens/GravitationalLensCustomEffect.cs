using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace GravitationalLens;

internal sealed class GravitationalLensCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    internal enum PropertyIndex
    {
        Amount = 0,
        LensX,
        LensY,
        HalfFieldTangent,
        ShadowAngle,
        TableStart,
        TableScale,
    }

    public float Amount { set => SetValue((int)PropertyIndex.Amount, value); }
    public float LensX { set => SetValue((int)PropertyIndex.LensX, value); }
    public float LensY { set => SetValue((int)PropertyIndex.LensY, value); }
    public float HalfFieldTangent { set => SetValue((int)PropertyIndex.HalfFieldTangent, value); }
    public float ShadowAngle { set => SetValue((int)PropertyIndex.ShadowAngle, value); }
    public float TableStart { set => SetValue((int)PropertyIndex.TableStart, value); }
    public float TableScale { set => SetValue((int)PropertyIndex.TableScale, value); }

    private const float DefaultHalfFieldTangent = 1f;
    private const float DefaultTableScale = 1f;
    private const float MaximumLensOffset = (float)GravitationalLensSettings.MaximumPositionPixels;

    [CustomEffect(2)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;
        private RawRect _tableRect = new(0, 0, GravitationalLensSettings.TableWidth, 1);

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Amount)]
        public float Amount { get => _cb.Amount; set { _cb.Amount = Sanitize(value, 0f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LensX)]
        public float LensX { get => _cb.LensX; set { _cb.LensX = Sanitize(value, -MaximumLensOffset, MaximumLensOffset); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LensY)]
        public float LensY { get => _cb.LensY; set { _cb.LensY = Sanitize(value, -MaximumLensOffset, MaximumLensOffset); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.HalfFieldTangent)]
        public float HalfFieldTangent { get => _cb.HalfFieldTangent; set { _cb.HalfFieldTangent = float.IsNaN(value) ? DefaultHalfFieldTangent : Math.Clamp(value, GravitationalLensSettings.MinimumHalfFieldTangent, GravitationalLensSettings.MaximumHalfFieldTangent); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShadowAngle)]
        public float ShadowAngle { get => _cb.ShadowAngle; set { _cb.ShadowAngle = Sanitize(value, 0f, (float)GravitationalLensSettings.MaximumShadowAngle); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.TableStart)]
        public float TableStart { get => _cb.TableStart; set { _cb.TableStart = float.IsFinite(value) ? value : 0f; UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.TableScale)]
        public float TableScale { get => _cb.TableScale; set { _cb.TableScale = float.IsFinite(value) ? Math.Max(value, 0f) : 0f; UpdateConstants(); } }

        public EffectImpl() : base(ShaderResourceUri.Get("GravitationalLens"))
        {
            _cb.HalfFieldTangent = DefaultHalfFieldTangent;
            _cb.TableScale = DefaultTableScale;
            _cb.TableWidth = GravitationalLensSettings.TableWidth;
        }

        private static float Sanitize(float value, float minimum, float maximum)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, minimum, maximum);

        protected override void UpdateConstants()
        {
            if (drawInformation is null)
                return;

            try
            {
                drawInformation.SetPixelShaderConstantBuffer(_cb);
            }
            catch (Exception exception)
            {
                GravitationalLensTelemetry.Report(exception);
                throw;
            }
        }

        public override void MapInputRectsToOutputRect(
            RawRect[] inputRects,
            RawRect[] inputOpaqueSubRects,
            out RawRect outputRect,
            out RawRect outputOpaqueSubRect)
        {
            inputRect = ClampInputRect(inputRects[0]);
            if (inputRects.Length > 1)
                _tableRect = inputRects[1];
            outputRect = inputRect;
            outputOpaqueSubRect = default;
            if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                return;

            _cb.InputBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
            UpdateConstants();
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            if (inputRects.Length > 0)
                inputRects[0] = inputRect;
            if (inputRects.Length > 1)
                inputRects[1] = _tableRect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public Vector4 InputBounds;
            public float LensX;
            public float LensY;
            public float HalfFieldTangent;
            public float Amount;
            public float ShadowAngle;
            public float TableStart;
            public float TableScale;
            public float TableWidth;
        }
    }
}
