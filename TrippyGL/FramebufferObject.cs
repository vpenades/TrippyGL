﻿using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace TrippyGL
{
    /// <summary>
    /// A configurable framebuffer that can be used to perform offscreen drawing operations
    /// </summary>
    public class FramebufferObject : GraphicsResource
    {
        /// <summary>The framebuffer's handle</summary>
        public readonly int Handle;

        /// <summary>The width of this renderbuffer's image</summary>
        public int Width { get; private set; }

        /// <summary>The height of this renderbuffer's image</summary>
        public int Height { get; private set; }

        /// <summary>The amount of samples this framebuffer has</summary>
        public int Samples { get; private set; }

        private List<FramebufferTextureAttachment> textureAttachments;
        private List<FramebufferRenderbufferAttachment> renderbufferAttachments;

        /// <summary>The amount of texture attachments this framebuffer has</summary>
        public int TextureAttachmentCount { get { return textureAttachments.Count; } }

        /// <summary>The amount of renderbuffer attachments this framebuffer has</summary>
        public int RenderbufferAttachmentCount { get { return renderbufferAttachments.Count; } }

        /// <summary>
        /// Creates a FramebufferObject
        /// </summary>
        /// <param name="graphicsDevice">The GraphicsDevice this resource will use</param>
        /// <param name="initialTextureAttachments">The initial length of the texture attachments list</param>
        /// <param name="initialRenderbufferAttachments">The initial length of the renderbuffer attachments list</param>
        public FramebufferObject(GraphicsDevice graphicsDevice, int initialTextureAttachments = 1, int initialRenderbufferAttachments = 1) : base(graphicsDevice)
        {
            this.Handle = GL.GenFramebuffer();
            this.Samples = 0;
            this.Width = 0;
            this.Height = 0;
            textureAttachments = new List<FramebufferTextureAttachment>(initialTextureAttachments);
            renderbufferAttachments = new List<FramebufferRenderbufferAttachment>(initialRenderbufferAttachments);
        }

        /// <summary>
        /// Attaches a texture to this framebuffer in a specified attachment point
        /// </summary>
        /// <param name="texture">The texture to attach</param>
        /// <param name="attachmentPoint">The attachment point to attach the texture to</param>
        public void Attach(Texture texture, FramebufferAttachmentPoint attachmentPoint)
        {
            ValidateAttachmentTypeExists(attachmentPoint);
            ValidateAttachmentTypeNotUsed(attachmentPoint);

            if (attachmentPoint == FramebufferAttachmentPoint.Depth && !TrippyUtils.IsImageFormatDepthType(texture.ImageFormat))
                throw new InvalidFramebufferAttachmentException("When attaching a texture to a depth attachment point, the texture's format must be depth-only");

            if (attachmentPoint == FramebufferAttachmentPoint.DepthStencil && !TrippyUtils.IsImageFormatDepthStencilType(texture.ImageFormat))
                throw new InvalidFramebufferAttachmentException("When attaching a texture to a depth-stencil attachment point, the texture's format must be depth-stencil");

            if (attachmentPoint == FramebufferAttachmentPoint.Stencil && !TrippyUtils.IsImageFormatStencilType(texture.ImageFormat))
                throw new InvalidFramebufferAttachmentException("When attaching a texture to a stencil attachment point, the texture's format must be stencil-only");

            if (TrippyUtils.IsFramebufferAttachmentPointColor(attachmentPoint) && !TrippyUtils.IsImageFormatColorRenderable(texture.ImageFormat))
                throw new InvalidFramebufferAttachmentException("When attaching a texture to a color attachment point, the texture's format must be color-renderable");

            GraphicsDevice.BindFramebuffer(this.Handle);
            if (texture is Texture1D)
            {
                GL.FramebufferTexture1D(FramebufferTarget.Framebuffer, (FramebufferAttachment)attachmentPoint, texture.TextureType, texture.Handle, 0);
            }
            else if (texture is Texture2D)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, (FramebufferAttachment)attachmentPoint, texture.TextureType, texture.Handle, 0);
            }
            else
                throw new InvalidFramebufferAttachmentException("This texture type cannot be attached to a framebuffer");
            textureAttachments.Add(new FramebufferTextureAttachment(texture, attachmentPoint));
        }

        /// <summary>
        /// Attaches a renderbuffer to this framebuffer in a specified attachment point
        /// </summary>
        /// <param name="renderbuffer">The renderbuffer to attach</param>
        /// <param name="attachmentPoint">The attachment point to attach the renderbuffer to</param>
        public void Attach(RenderbufferObject renderbuffer, FramebufferAttachmentPoint attachmentPoint)
        {
            ValidateAttachmentTypeExists(attachmentPoint);
            ValidateAttachmentTypeNotUsed(attachmentPoint);

            if (attachmentPoint == FramebufferAttachmentPoint.Depth && !renderbuffer.IsDepthOnly)
                throw new InvalidFramebufferAttachmentException("When attaching a renderbuffer to a depth attachment point, the renderbuffer's format must be depth-only");

            if (attachmentPoint == FramebufferAttachmentPoint.DepthStencil && !renderbuffer.IsDepthStencil)
                throw new InvalidFramebufferAttachmentException("When attaching a renderbuffer to a depth-stencil attachment point, the renderbuffer's format must be depth-stencil");

            if (attachmentPoint == FramebufferAttachmentPoint.Stencil && !renderbuffer.IsStencilOnly)
                throw new InvalidFramebufferAttachmentException("When attaching a renderbuffer to a stencil attachment point, the renderbuffer's format must be stencil-only");

            if (TrippyUtils.IsFramebufferAttachmentPointColor(attachmentPoint) && !renderbuffer.IsColorRenderableFormat)
                throw new InvalidFramebufferAttachmentException("When attaching a renderbuffer to a color attachment point, the renderbuffer's format must be color-renderable");

            GraphicsDevice.BindFramebuffer(this.Handle);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, (FramebufferAttachment)attachmentPoint, RenderbufferTarget.Renderbuffer, renderbuffer.Handle);
            renderbufferAttachments.Add(new FramebufferRenderbufferAttachment(renderbuffer, attachmentPoint));
        }

        /// <summary>
        /// Detaches whatever is in an attachment point. Throws an exception if there is no such attachment
        /// </summary>
        /// <param name="attachmentPoint">The attachment point to clear</param>
        public void Detach(FramebufferAttachmentPoint attachmentPoint)
        {
            if (!TryDetachTexture(attachmentPoint, out _))
                if (!TryDetachRenderbuffer(attachmentPoint, out _))
                    throw new InvalidOperationException("The specified attachment point is empty");
        }

        /// <summary>
        /// Tries to detach a texture attached to the specified point. Returns whether the operation succeeded
        /// </summary>
        /// <param name="point">The attachment point to check</param>
        /// <param name="attachment">The detached attachment, if the method returns true</param>
        public bool TryDetachTexture(FramebufferAttachmentPoint point, out FramebufferTextureAttachment attachment)
        {
            for (int i = 0; i < textureAttachments.Count; i++)
                if (textureAttachments[i].AttachmentPoint == point)
                {
                    GraphicsDevice.BindFramebuffer(this.Handle);
                    GL.FramebufferTexture(FramebufferTarget.Framebuffer, (FramebufferAttachment)point, 0, 0);
                    attachment = textureAttachments[i];
                    textureAttachments.RemoveAt(i);
                    return true;
                }
            attachment = default;
            return false;
        }

        /// <summary>
        /// Tries to detach a renderbuffer attached to the specified point. Returns whether the operation succeded
        /// </summary>
        /// <param name="point">The attachment point to check</param>
        /// <param name="attachment">The detached attachment, if the method returns true</param>
        public bool TryDetachRenderbuffer(FramebufferAttachmentPoint point, out FramebufferRenderbufferAttachment attachment)
        {
            for (int i = 0; i < renderbufferAttachments.Count; i++)
                if (renderbufferAttachments[i].AttachmentPoint == point)
                {
                    GraphicsDevice.BindFramebuffer(this.Handle);
                    GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, (FramebufferAttachment)point, RenderbufferTarget.Renderbuffer, 0);
                    attachment = renderbufferAttachments[i];
                    renderbufferAttachments.RemoveAt(i);
                    return true;
                }
            attachment = default;
            return false;
        }

        /// <summary>
        /// Returns whether the specified attachment point is in use
        /// </summary>
        /// <param name="attachmentType">The attachment point to check</param>
        public bool HasAttachment(FramebufferAttachmentPoint attachmentType)
        {
            for (int i = 0; i < textureAttachments.Count; i++)
                if (textureAttachments[i].AttachmentPoint == attachmentType)
                    return true;
            for (int i = 0; i < renderbufferAttachments.Count; i++)
                if (renderbufferAttachments[i].AttachmentPoint == attachmentType)
                    return true;
            return false;
        }

        /// <summary>
        /// Gets the status of the framebuffer. 
        /// </summary>
        public FramebufferErrorCode GetStatus()
        {
            GraphicsDevice.BindFramebuffer(this.Handle);
            return GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        }

        /// <summary>
        /// Updates the framebuffer's parameters and checks that the framebuffer is valid.
        /// This should always be called after being done attaching or detaching resources
        /// </summary>
        public void UpdateFramebufferData()
        {
            int width = -1;
            int height = -1;
            int samples = -1;

            for (int i = 0; i < textureAttachments.Count; i++)
            {
                Texture tex = textureAttachments[i].Texture;
                if (tex is Texture1D)
                    ValidateSize(((Texture1D)tex).Width, 1);
                else if (tex is Texture2D tex2d)
                {
                    ValidateSize(tex2d.Width, tex2d.Height);
                }
                else
                    throw new FramebufferException("The texture format cannot be attached: " + tex.TextureType);

                IMultisamplableTexture ms = tex as IMultisamplableTexture;
                ValidateSamples(ms == null ? 0 : ms.Samples);
            }

            for (int i = 0; i < renderbufferAttachments.Count; i++)
            {
                RenderbufferObject rend = renderbufferAttachments[i].Renderbuffer;
                ValidateSize(rend.Width, rend.Height);
                ValidateSamples(rend.Samples);
            }

            this.Width = width;
            this.Height = height;
            this.Samples = samples;

            void ValidateSize(int w, int h)
            {
                if (width == -1)
                    width = w;
                else if (width != w)
                    throw new FramebufferException("All the framebuffer's attachments must be the same size");

                if (height == -1)
                    height = h;
                else if (height != h)
                    throw new FramebufferException("All the framebuffer's attachments must be the same size");
            }

            void ValidateSamples(int s)
            {
                if (samples == -1)
                    samples = s;
                else if (samples != s)
                    throw new FramebufferException("All the framebuffer attachments must have the same amount of samples");
            }

            FramebufferErrorCode c = GetStatus();
            if (c != FramebufferErrorCode.FramebufferComplete)
                throw new FramebufferException("The framebuffer is not complete: " + c);
        }

        /// <summary>
        /// Gets a texture attachment from this framebuffer
        /// </summary>
        /// <param name="index">The enumeration index for the texture attachment</param>
        public FramebufferTextureAttachment GetTextureAttachment(int index)
        {
            return textureAttachments[index];
        }

        /// <summary>
        /// Gets a renderbuffer attachment from this framebuffer
        /// </summary>
        /// <param name="index">The enumeration index for the renderbuffer attachment</param>
        public FramebufferRenderbufferAttachment GetRenderbufferAttachment(int index)
        {
            return renderbufferAttachments[index];
        }

        /// <summary>
        /// Saves this texture as an image file. You can't save multisampled textures
        /// </summary>
        /// <param name="file">The location in which to store the file</param>
        /// <param name="imageFormat">The format</param>
        public void SaveAsImage(string file, SaveImageFormat imageFormat)
        {
            if (String.IsNullOrEmpty(file))
                throw new ArgumentException("You must specify a file name", "file");

            ImageFormat format;

            switch (imageFormat)
            {
                case SaveImageFormat.Png:
                    format = ImageFormat.Png;
                    break;
                case SaveImageFormat.Jpeg:
                    format = ImageFormat.Jpeg;
                    break;
                case SaveImageFormat.Bmp:
                    format = ImageFormat.Bmp;
                    break;
                case SaveImageFormat.Tiff:
                    format = ImageFormat.Tiff;
                    break;
                default:
                    throw new ArgumentException("You must use a proper value from SaveImageFormat", "imageFormat");
            }

            using (Bitmap b = new Bitmap(this.Width, this.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                BitmapData data = b.LockBits(new System.Drawing.Rectangle(0, 0, this.Width, this.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GraphicsDevice.BindFramebufferRead(this);
                GL.ReadPixels(0, 0, this.Width, this.Height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
                b.UnlockBits(data);
                b.RotateFlip(RotateFlipType.RotateNoneFlipY);
                b.Save(file, ImageFormat.Png);
            }
        }

        public override string ToString()
        {
            return String.Concat("Handle=", Handle, ", Width=", Width, ", Height=", Height, ", Samples=", Samples, ", TextureAttachments=", TextureAttachmentCount, ", RenderbufferAttachments=", RenderbufferAttachmentCount);
        }

        protected override void Dispose(bool isManualDispose)
        {
            GL.DeleteFramebuffer(this.Handle);
            base.Dispose(isManualDispose);
        }

        /// <summary>
        /// Disposes all of the attachments
        /// </summary>
        public void DisposeAttachments()
        {
            for (int i = 0; i < textureAttachments.Count; i++)
                textureAttachments[i].Texture.Dispose();
            textureAttachments.Clear();
            for (int i = 0; i < renderbufferAttachments.Count; i++)
                renderbufferAttachments[i].Renderbuffer.Dispose();
            renderbufferAttachments.Clear();
        }

        private void ValidateAttachmentTypeExists(FramebufferAttachmentPoint attachment)
        {
            if (!Enum.IsDefined(typeof(FramebufferAttachmentPoint), attachment))
                throw new FormatException("Invalid attachment point");
        }

        private void ValidateAttachmentTypeNotUsed(FramebufferAttachmentPoint attachment)
        {
            if (HasAttachment(attachment))
                throw new InvalidOperationException("The framebuffer already has this type of attachment");
        }

        /// <summary>
        /// Creates a typical 2D framebuffer with a 2D texture and, if specified, depth and/or stencil
        /// </summary>
        /// <param name="texture">The texture to which the framebuffer will draw to. If null, it will be generated. Otherwise, if necessary, it's image will be recreated to the appropiate size</param>
        /// <param name="graphicsDevice">The GraphicsDevice this resource will use</param>
        /// <param name="width">The width of the framebuffer's image</param>
        /// <param name="height">The height of the framebuffer's image</param>
        /// <param name="depthStencilFormat">The desired depth-stencil format for the framebuffer, which will be attached as a renderbuffer</param>
        /// <param name="samples">The amount of samples for the framebuffer</param>
        /// <param name="imageFormat">The image format for this framebuffer's texture</param>
        public static FramebufferObject Create2D(ref Texture2D texture, GraphicsDevice graphicsDevice, int width, int height, DepthStencilFormat depthStencilFormat, int samples = 0, TextureImageFormat imageFormat = TextureImageFormat.Color4b)
        {
            if (texture == null)
                texture = new Texture2D(graphicsDevice, width, height, false, samples, imageFormat);
            else if (texture.Width != width || texture.Height != height)
                texture.RecreateImage(width, height);

            FramebufferObject fbo = new FramebufferObject(graphicsDevice);
            fbo.Attach(texture, FramebufferAttachmentPoint.Color0);
            if (depthStencilFormat != DepthStencilFormat.None)
            {
                RenderbufferObject rbo = new RenderbufferObject(graphicsDevice, width, height, (RenderbufferFormat)depthStencilFormat, samples);
                fbo.Attach(rbo,
                    TrippyUtils.IsDepthStencilFormatDepthAndStencil(depthStencilFormat) ? FramebufferAttachmentPoint.DepthStencil :
                    (TrippyUtils.IsDepthStencilFormatDepthOnly(depthStencilFormat) ? FramebufferAttachmentPoint.Depth : FramebufferAttachmentPoint.Stencil));
            }
            fbo.UpdateFramebufferData();
            return fbo;
        }

        /// <summary>
        /// Performs a resize on a typical 2D framebuffer. All texture attachments (which must be Texture2D-s) will be
        /// resized and all renderbuffers will be disposed and recreated with the new size
        /// </summary>
        /// <param name="framebuffer">The framebuffer to resize</param>
        /// <param name="width">The new width</param>
        /// <param name="height">The new height</param>
        public static void Resize2D(FramebufferObject framebuffer, int width, int height)
        {
            for (int i = 0; i < framebuffer.textureAttachments.Count; i++)
            {
                if (!(framebuffer.textureAttachments[i].Texture is Texture2D tex2d))
                    throw new FramebufferException("This framebuffer contains non-Texture2D texture attachments, a Resize2D operation is invalid");
                tex2d.RecreateImage(width, height);
            }

            for (int i = 0; i < framebuffer.renderbufferAttachments.Count; i++)
            {
                FramebufferRenderbufferAttachment att = framebuffer.renderbufferAttachments[i];
                framebuffer.TryDetachRenderbuffer(att.AttachmentPoint, out att);
                att.Renderbuffer.Dispose();
                framebuffer.Attach(new RenderbufferObject(framebuffer.GraphicsDevice, width, height, att.Renderbuffer.Format, att.Renderbuffer.Samples), att.AttachmentPoint);
            }

            framebuffer.UpdateFramebufferData();
        }
    }

    public struct FramebufferTextureAttachment
    {
        public readonly Texture Texture;
        public readonly FramebufferAttachmentPoint AttachmentPoint;

        public FramebufferTextureAttachment(Texture texture, FramebufferAttachmentPoint attachmentPoint)
        {
            this.Texture = texture;
            this.AttachmentPoint = attachmentPoint;
        }
    }

    public struct FramebufferRenderbufferAttachment
    {
        public readonly RenderbufferObject Renderbuffer;
        public readonly FramebufferAttachmentPoint AttachmentPoint;

        public FramebufferRenderbufferAttachment(RenderbufferObject renderbuffer, FramebufferAttachmentPoint attachmentPoint)
        {
            this.Renderbuffer = renderbuffer;
            this.AttachmentPoint = attachmentPoint;
        }
    }
}
