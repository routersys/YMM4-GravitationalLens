using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using Vortice.DCommon;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace GravitationalLens;

internal sealed class GravitationalLensEffectProcessor(IGraphicsDevicesAndContext devices, GravitationalLensEffect item) : VideoEffectProcessorBase(devices)
{
    private readonly IGraphicsDevicesAndContext _devices = devices;
    private readonly GravitationalLensEffect _item = item;
    private GravitationalLensCustomEffect? _effect;
    private ID2D1Bitmap1? _table;
    private bool _isFirst = true;
    private double _distance;
    private ConstantKey _constantKey;
    private ImmutableList<VideoEffectController> _controllers = [];
    private Vector2 _controllerPosition = new(float.NaN, float.NaN);

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        try
        {
            return UpdateCore(effectDescription);
        }
        catch (Exception exception)
        {
            GravitationalLensTelemetry.Report(exception);
            throw;
        }
    }

    private DrawDescription UpdateCore(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _effect is null || _table is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;
        var amount = (float)(_item.Amount.GetValue(frame, length, fps) / 100.0);
        var limit = GravitationalLensSettings.MaximumPositionPixels;
        var lensX = (float)Math.Clamp(_item.PositionX.GetValue(frame, length, fps), -limit, limit);
        var lensY = (float)Math.Clamp(_item.PositionY.GetValue(frame, length, fps), -limit, limit);
        var draw = WithController(effectDescription.DrawDescription, lensX, lensY);
        if (_isFirst || _constantKey.Amount != amount)
            _effect.Amount = amount;

        if (amount <= 0f)
        {
            _constantKey = _constantKey with { Amount = amount };
            _isFirst = false;
            return draw;
        }

        var distance = GravitationalLensSettings.ClampDistance(_item.Distance.GetValue(frame, length, fps));
        var shadowAngle = _constantKey.ShadowAngle;
        var tableStart = _constantKey.TableStart;
        var tableScale = _constantKey.TableScale;
        if (_isFirst || _distance != distance)
        {
            var table = GravitationalLensTable.Build(distance);
            WriteTable(table.ToTexels());
            _distance = distance;
            shadowAngle = (float)table.ShadowAngle;
            tableStart = (float)table.LogStart;
            tableScale = (float)(1.0 / table.LogStep);
        }

        var constantKey = new ConstantKey(
            amount,
            lensX,
            lensY,
            GravitationalLensSettings.GetHalfFieldOfViewTangent(_item.FieldOfView.GetValue(frame, length, fps)),
            shadowAngle,
            tableStart,
            tableScale);
        if (_isFirst || _constantKey.LensX != constantKey.LensX)
            _effect.LensX = constantKey.LensX;
        if (_isFirst || _constantKey.LensY != constantKey.LensY)
            _effect.LensY = constantKey.LensY;
        if (_isFirst || _constantKey.HalfFieldTangent != constantKey.HalfFieldTangent)
            _effect.HalfFieldTangent = constantKey.HalfFieldTangent;
        if (_isFirst || _constantKey.ShadowAngle != constantKey.ShadowAngle)
            _effect.ShadowAngle = constantKey.ShadowAngle;
        if (_isFirst || _constantKey.TableStart != constantKey.TableStart)
            _effect.TableStart = constantKey.TableStart;
        if (_isFirst || _constantKey.TableScale != constantKey.TableScale)
            _effect.TableScale = constantKey.TableScale;

        _constantKey = constantKey;
        _isFirst = false;
        return draw;
    }

    private DrawDescription WithController(DrawDescription draw, float lensX, float lensY)
    {
        if (input is null)
            return draw;

        var bounds = _devices.DeviceContext.GetImageLocalBounds(input);
        var position = new Vector2((bounds.Left + bounds.Right) * 0.5f + lensX, (bounds.Top + bounds.Bottom) * 0.5f + lensY);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            return draw;

        if (_controllerPosition != position)
        {
            var point = new ControllerPoint(new Vector3(position, 0f), OnDrag);
            _controllers = [new VideoEffectController(_item, [point])];
            _controllerPosition = position;
        }

        return draw with { Controllers = draw.Controllers.AddRange(_controllers) };
    }

    private void OnDrag(ControlPointDragEventArgs argument)
    {
        _item.PositionX.AddToEachValues(argument.Delta.X);
        _item.PositionY.AddToEachValues(argument.Delta.Y);
    }

    private void WriteTable(float[] texels)
    {
        var handle = GCHandle.Alloc(texels, GCHandleType.Pinned);
        try
        {
            _table!.CopyFromMemory(handle.AddrOfPinnedObject(), GravitationalLensSettings.TableWidth * GravitationalLensSettings.TexelBytes);
        }
        finally
        {
            handle.Free();
        }
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var effect = new GravitationalLensCustomEffect(devices);
        if (!effect.IsEnabled)
        {
            effect.Dispose();
            return null;
        }

        _effect = effect;
        disposer.Collect(effect);
        var properties = new BitmapProperties1(new PixelFormat(Format.R32G32B32A32_Float, Vortice.DCommon.AlphaMode.Premultiplied), 96f, 96f, BitmapOptions.None);
        var empty = new float[GravitationalLensSettings.TableWidth * GravitationalLensSettings.TexelChannels];
        var handle = GCHandle.Alloc(empty, GCHandleType.Pinned);
        try
        {
            _table = ((ID2D1DeviceContext1)devices.DeviceContext).CreateBitmap(
                new Vortice.Mathematics.SizeI(GravitationalLensSettings.TableWidth, 1),
                handle.AddrOfPinnedObject(),
                GravitationalLensSettings.TableWidth * GravitationalLensSettings.TexelBytes,
                properties);
        }
        finally
        {
            handle.Free();
        }

        disposer.Collect(_table);
        _effect.SetInput(1, _table, true);
        var output = effect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _effect?.SetInput(0, input, true);
    }

    protected override void ClearEffectChain()
    {
        try
        {
            _effect?.SetInput(0, null, true);
            _isFirst = true;
        }
        catch (Exception exception)
        {
            GravitationalLensTelemetry.Report(exception);
            throw;
        }
    }

    private readonly record struct ConstantKey(float Amount, float LensX, float LensY, float HalfFieldTangent, float ShadowAngle, float TableStart, float TableScale);
}
