using System;
using CefSharp;
using CefSharp.Enums;
using CefSharp.OffScreen;
using CefSharp.Structs;

namespace StreamingRespirator.Core.CefHelper
{
    internal class NullRenderHandler : IRenderHandler
    {
        void IDisposable.Dispose()
        {
        }

        ScreenInfo? IRenderHandler.GetScreenInfo()
        {
            return new ScreenInfo { DeviceScaleFactor = 1.0F };
        }

        Rect? IRenderHandler.GetViewRect()
        {
            return new Rect(0, 0, 0, 0);
        }

        bool IRenderHandler.GetScreenPoint(int viewX, int viewY, out int screenX, out int screenY)
        {
            screenX = viewX;
            screenY = viewY;

            return false;
        }

        void IRenderHandler.OnPaint(PaintElementType type, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
        }

        void IRenderHandler.OnCursorChange(IntPtr cursor, CursorType type, CursorInfo customCursorInfo)
        {

        }

        bool IRenderHandler.StartDragging(IDragData dragData, DragOperationsMask mask, int x, int y)
        {
            return false;
        }

        void IRenderHandler.UpdateDragCursor(DragOperationsMask operation)
        {
        }

        void IRenderHandler.OnPopupShow(bool show)
        {
        }

        void IRenderHandler.OnPopupSize(Rect rect)
        {
        }

        void IRenderHandler.OnImeCompositionRangeChanged(Range selectedRange, Rect[] characterBounds)
        {
        }
    }
}
