
#define HISTOGRAM_BINS 128          // IMPORTANT: If this number is changed, the code needs adapting, I tried to add relevant comments to indicate where.

#define _HistogramRangeScale     _HistogramExposureParams.x
#define _HistogramRangeBias      _HistogramExposureParams.y
#define _HistogramMinPercentile  _HistogramExposureParams.z
#define _HistogramMaxPercentile  _HistogramExposureParams.w

#ifdef GEN_PASS
RWStructuredBuffer<uint> _HistogramBuffer;
#else
StructuredBuffer<uint> _HistogramBuffer;
#endif

float UnpackWeight(uint val)
{
    return val * rcp(2048.0f);
}

float GetFractionWithinHistogram(float value)
{
    return ComputeEV100FromAvgLuminance(value) * _HistogramRangeScale + _HistogramRangeBias;
}

uint GetHistogramBinLocation(float value)
{
    return uint(saturate(GetFractionWithinHistogram(value)) * (HISTOGRAM_BINS - 1));
}

float BinLocationToEV(uint binIdx)
{
    return (binIdx * rcp(float(HISTOGRAM_BINS - 1)) - _HistogramRangeBias) / _HistogramRangeScale;
}
