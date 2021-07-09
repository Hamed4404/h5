// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Generated by the protocol buffer compiler.  DO NOT EDIT!
// source: WeatherReport.proto
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace SocialWeather.Protobuf {

  /// <summary>Holder for reflection information generated from WeatherReport.proto</summary>
  public static partial class WeatherReportReflection {

    #region Descriptor
    /// <summary>File descriptor for WeatherReport.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static readonly pbr::FileDescriptor descriptor;

    static WeatherReportReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChNXZWF0aGVyUmVwb3J0LnByb3RvIuIBCg1XZWF0aGVyUmVwb3J0EhMKC1Rl",
            "bXBlcmF0dXJlGAEgASgFEhIKClJlcG9ydFRpbWUYAiABKAMSKwoHV2VhdGhl",
            "chgDIAEoDjIaLldlYXRoZXJSZXBvcnQuV2VhdGhlcktpbmQSDwoHWmlwQ29k",
            "ZRgEIAEoCSJqCgtXZWF0aGVyS2luZBIJCgVTdW5ueRAAEg8KC01vc3RseVN1",
            "bm55EAESDwoLUGFydGx5U3VubnkQAhIQCgxQYXJ0bHlDbG91ZHkQAxIQCgxN",
            "b3N0bHlDbG91ZHkQBBIKCgZDbG91ZHkQBUIZqgIWU29jaWFsV2VhdGhlci5Q",
            "cm90b2J1ZmIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::SocialWeather.Protobuf.WeatherReport), global::SocialWeather.Protobuf.WeatherReport.Parser, new[]{ "Temperature", "ReportTime", "Weather", "ZipCode" }, null, new[]{ typeof(global::SocialWeather.Protobuf.WeatherReport.Types.WeatherKind) }, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class WeatherReport : pb::IMessage<WeatherReport> {
    private static readonly pb::MessageParser<WeatherReport> _parser = new pb::MessageParser<WeatherReport>(() => new WeatherReport());
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<WeatherReport> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::SocialWeather.Protobuf.WeatherReportReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public WeatherReport() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public WeatherReport(WeatherReport other) : this() {
      temperature_ = other.temperature_;
      reportTime_ = other.reportTime_;
      weather_ = other.weather_;
      zipCode_ = other.zipCode_;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public WeatherReport Clone() {
      return new WeatherReport(this);
    }

    /// <summary>Field number for the "Temperature" field.</summary>
    public const int TemperatureFieldNumber = 1;
    private int temperature_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int Temperature {
      get { return temperature_; }
      set {
        temperature_ = value;
      }
    }

    /// <summary>Field number for the "ReportTime" field.</summary>
    public const int ReportTimeFieldNumber = 2;
    private long reportTime_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public long ReportTime {
      get { return reportTime_; }
      set {
        reportTime_ = value;
      }
    }

    /// <summary>Field number for the "Weather" field.</summary>
    public const int WeatherFieldNumber = 3;
    private global::SocialWeather.Protobuf.WeatherReport.Types.WeatherKind weather_ = 0;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::SocialWeather.Protobuf.WeatherReport.Types.WeatherKind Weather {
      get { return weather_; }
      set {
        weather_ = value;
      }
    }

    /// <summary>Field number for the "ZipCode" field.</summary>
    public const int ZipCodeFieldNumber = 4;
    private string zipCode_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string ZipCode {
      get { return zipCode_; }
      set {
        zipCode_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as WeatherReport);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(WeatherReport other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Temperature != other.Temperature) return false;
      if (ReportTime != other.ReportTime) return false;
      if (Weather != other.Weather) return false;
      if (ZipCode != other.ZipCode) return false;
      return true;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (Temperature != 0) hash ^= Temperature.GetHashCode();
      if (ReportTime != 0L) hash ^= ReportTime.GetHashCode();
      if (Weather != 0) hash ^= Weather.GetHashCode();
      if (ZipCode.Length != 0) hash ^= ZipCode.GetHashCode();
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      if (Temperature != 0) {
        output.WriteRawTag(8);
        output.WriteInt32(Temperature);
      }
      if (ReportTime != 0L) {
        output.WriteRawTag(16);
        output.WriteInt64(ReportTime);
      }
      if (Weather != 0) {
        output.WriteRawTag(24);
        output.WriteEnum((int) Weather);
      }
      if (ZipCode.Length != 0) {
        output.WriteRawTag(34);
        output.WriteString(ZipCode);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (Temperature != 0) {
        size += 1 + pb::CodedOutputStream.ComputeInt32Size(Temperature);
      }
      if (ReportTime != 0L) {
        size += 1 + pb::CodedOutputStream.ComputeInt64Size(ReportTime);
      }
      if (Weather != 0) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) Weather);
      }
      if (ZipCode.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(ZipCode);
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(WeatherReport other) {
      if (other == null) {
        return;
      }
      if (other.Temperature != 0) {
        Temperature = other.Temperature;
      }
      if (other.ReportTime != 0L) {
        ReportTime = other.ReportTime;
      }
      if (other.Weather != 0) {
        Weather = other.Weather;
      }
      if (other.ZipCode.Length != 0) {
        ZipCode = other.ZipCode;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            input.SkipLastField();
            break;
          case 8: {
            Temperature = input.ReadInt32();
            break;
          }
          case 16: {
            ReportTime = input.ReadInt64();
            break;
          }
          case 24: {
            weather_ = (global::SocialWeather.Protobuf.WeatherReport.Types.WeatherKind) input.ReadEnum();
            break;
          }
          case 34: {
            ZipCode = input.ReadString();
            break;
          }
        }
      }
    }

    #region Nested types
    /// <summary>Container for nested types declared in the WeatherReport message type.</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static partial class Types {
      public enum WeatherKind {
        [pbr::OriginalName("Sunny")] Sunny = 0,
        [pbr::OriginalName("MostlySunny")] MostlySunny = 1,
        [pbr::OriginalName("PartlySunny")] PartlySunny = 2,
        [pbr::OriginalName("PartlyCloudy")] PartlyCloudy = 3,
        [pbr::OriginalName("MostlyCloudy")] MostlyCloudy = 4,
        [pbr::OriginalName("Cloudy")] Cloudy = 5,
      }

    }
    #endregion

  }

  #endregion

}

#endregion Designer generated code
