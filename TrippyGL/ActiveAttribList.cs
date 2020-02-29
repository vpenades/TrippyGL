using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;

namespace TrippyGL
{
    /// <summary>
    /// A readonly list containing the active attributes of a <see cref="ShaderProgram"/>.
    /// </summary>
    public class ActiveAttribList
    {
        /// <summary>The internal <see cref="ActiveVertexAttrib"/> array.</summary>
        private readonly ActiveVertexAttrib[] attributes;

        /// <summary>
        /// Gets an <see cref="ActiveVertexAttrib"/> from the list.
        /// While these are ordered by location, remember that some attributes use more than one location.
        /// </summary>
        /// <param name="index">The list index of the <see cref="ActiveVertexAttrib"/>.</param>
        public ActiveVertexAttrib this[int index] => attributes[index];

        /// <summary>The amount of <see cref="ActiveVertexAttrib"/>-s stored by this list.</summary>
        public int Length => attributes.Length;

        /// <summary>
        /// Creates an <see cref="ActiveAttribList"/> where the attribute list is queried from a <see cref="ShaderProgram"/>.
        /// </summary>
        /// <param name="program">The <see cref="ShaderProgram"/> to query the attributes from.</param>
        internal ActiveAttribList(ShaderProgram program)
        {
            // We query the total amount of attributes we'll be reading from OpenGL
            GL.GetProgram(program.Handle, GetProgramParameterName.ActiveAttributes, out int attribCount);

            // We'll be storing the attributes in this list and then turning it into an array, because we can't
            // know for sure how many attributes we'll have at the end, we just know it's be <= than attribCount
            List<ActiveVertexAttrib> attribList = new List<ActiveVertexAttrib>(attribCount);

            // We query all the ShaderProgram's attributes one by one and add them to attribList
            for (int i = 0; i < attribCount; i++)
            {
                ActiveVertexAttrib a = new ActiveVertexAttrib(program, i);
                if (a.Location >= 0)    // Sometimes other stuff shows up, such as gl_InstanceID with location -1.
                    attribList.Add(a);  // We should, of course, filter these out.
            }

            attributes = attribList.ToArray();

            // The attributes don't always appear ordered by location, so let's order them now
            Array.Sort(attributes, (x, y) => x.Location.CompareTo(y.Location));
        }

        /// <summary>
        /// Checks that the names given for some vertex attributes match the names actually found for the vertex attributes.
        /// </summary>
        /// <param name="providedDesc">The <see cref="VertexAttribDescription"/>-s provided.</param>
        /// <param name="providedNames">The names of the <see cref="VertexAttribDescription"/>-s provided</param>
        internal bool DoAttributesMatch(Span<VertexAttribDescription> providedDesc, Span<string> providedNames)
        {
            // This function assumes the length of the two given arrays match

            // While all of the attribute names are provided by the user, that doesn't mean all of them are in here.
            // The GLSL compiler may not make an attribute ACTIVE if, for example, it is never used.
            // So, if we see a provided name doesn't match, maybe it isn't active, so let's skip that name and check the next.
            // That said, both arrays are indexed in the same way. So if all attributes are active, we'll basically just
            // check one-by-one, index-by-index that the names on attributes[i] match providedNames[i]

            int nameIndex = 0;

            if (providedNames.Length == 0)
                return attributes.Length == 0;

            for (int i = 0; i < attributes.Length; i++)
            {
                if (nameIndex == providedNames.Length)
                    return false;

                while (providedDesc[nameIndex].AttribType != attributes[i].AttribType || attributes[i].Name != providedNames[nameIndex])
                {
                    if (++nameIndex == providedNames.Length)
                        return false;
                }
                nameIndex++;
            }

            return true;
        }
    }
}
