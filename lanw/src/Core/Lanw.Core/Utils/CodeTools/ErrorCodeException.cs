using Lanw.Core.Entities;

namespace Lanw.Core.Utils.CodeTools;

public class ErrorCodeException(ErrorCode errorCode, object? data = null) : Exception(Code.GetMessage(errorCode)) {
    public readonly EntityResponse<object> Entity = Code.ToJson(errorCode, data);

    public ErrorCodeException() : this(ErrorCode.Failure) { }

    public ErrorCodeException(object? data = null) : this(ErrorCode.Failure, data) { }

    public EntityResponse<object> GetJson()
    {
        return Entity;
    }
}