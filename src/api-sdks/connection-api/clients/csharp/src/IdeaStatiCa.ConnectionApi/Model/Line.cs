/*
 * Connection Rest API 1.0
 *
 * API for designing steel connections
 *
 * The version of the OpenAPI document: 1.0
 * Contact: info@ideastatica.com
 * Generated by: https://github.com/openapitools/openapi-generator.git
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using OpenAPIDateConverter = IdeaStatiCa.ConnectionApi.Client.OpenAPIDateConverter;

namespace IdeaStatiCa.ConnectionApi.Model
{
    /// <summary>
    /// Line
    /// </summary>
    [DataContract(Name = "Line")]
    public partial class Line
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Line" /> class.
        /// </summary>
        /// <param name="color">color.</param>
        /// <param name="pairs">pairs.</param>
        /// <param name="thickness">thickness.</param>
        public Line(List<int> color = default(List<int>), List<int> pairs = default(List<int>), double thickness = default(double))
        {
            this.Color = color;
            this.Pairs = pairs;
            this.Thickness = thickness;
        }

        /// <summary>
        /// Gets or Sets Color
        /// </summary>
        [DataMember(Name = "color", EmitDefaultValue = true)]
        public List<int> Color { get; set; }

        /// <summary>
        /// Gets or Sets Pairs
        /// </summary>
        [DataMember(Name = "pairs", EmitDefaultValue = true)]
        public List<int> Pairs { get; set; }

        /// <summary>
        /// Gets or Sets Thickness
        /// </summary>
        [DataMember(Name = "thickness", EmitDefaultValue = false)]
        public double Thickness { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("class Line {\n");
            sb.Append("  Color: ").Append(Color).Append("\n");
            sb.Append("  Pairs: ").Append(Pairs).Append("\n");
            sb.Append("  Thickness: ").Append(Thickness).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }

    }

}