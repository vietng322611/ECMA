using System.ComponentModel.DataAnnotations;
using ECMA.Domain.Enums;
using ECMA.Web.Services;

namespace ECMA.Web.Models;

public sealed class EventManageListViewModel
{
    public string? Keyword { get; init; }
    public string? Type { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public IReadOnlyList<EventListItemViewModel> Events { get; init; } = [];
}

public sealed class EventListItemViewModel
{
    public Guid Id { get; init; }
    public EventType Type { get; init; }
    public string Title { get; init; } = "";
    public string Location { get; init; } = "";
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public int Capacity { get; init; }
    public EffectiveEventStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class EventDetailsViewModel
{
    public Guid Id { get; init; }
    public Guid OwnerId { get; init; }
    public EventType Type { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Location { get; init; } = "";
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public int Capacity { get; init; }
    public EffectiveEventStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CancelReason { get; init; }
    public bool CanEditPublished { get; init; }
    public bool CanCancel { get; init; }
    public bool CanManage { get; init; }
    public IReadOnlyList<AgendaItemViewModel> Agenda { get; init; } = [];
}

public sealed class AgendaItemViewModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Content { get; init; }
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
}

public sealed class EventFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Loại sự kiện là bắt buộc")]
    public EventType? Type { get; set; }

    [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả là bắt buộc")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Số nhà, tòa nhà hoặc tên đường là bắt buộc")]
    public string StreetAddress { get; set; } = "";

    [Required(ErrorMessage = "Tỉnh/thành là bắt buộc")]
    public string ProvinceCode { get; set; } = "";

    [Required(ErrorMessage = "Xã/phường là bắt buộc")]
    public string WardCode { get; set; } = "";

    public IReadOnlyList<VietnamLocationCatalog.Province> Provinces { get; set; } = [];

    [Required(ErrorMessage = "Thời gian bắt đầu là bắt buộc"), DataType(DataType.DateTime)]
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "Thời gian kết thúc là bắt buộc"), DataType(DataType.DateTime)]
    public DateTime? EndAt { get; set; }

    [Range(1, 10000, ErrorMessage = "Sức chứa phải từ 1 đến 10000")]
    public int Capacity { get; set; } = 1;
}

public sealed class PublishedEventFormViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả là bắt buộc")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Số nhà, tòa nhà hoặc tên đường là bắt buộc")]
    public string StreetAddress { get; set; } = "";

    [Required(ErrorMessage = "Tỉnh/thành là bắt buộc")]
    public string ProvinceCode { get; set; } = "";

    [Required(ErrorMessage = "Xã/phường là bắt buộc")]
    public string WardCode { get; set; } = "";

    public IReadOnlyList<VietnamLocationCatalog.Province> Provinces { get; set; } = [];

    [Range(1, 10000, ErrorMessage = "Sức chứa phải từ 1 đến 10000")]
    public int Capacity { get; set; }

    public string OriginalLocation { get; set; } = "";
}

public sealed class AgendaFormViewModel
{
    public Guid EventId { get; set; }
    public Guid? ItemId { get; set; }

    [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
    public string Title { get; set; } = "";

    public string? Content { get; set; }

    [Required(ErrorMessage = "Thời gian bắt đầu là bắt buộc"), DataType(DataType.DateTime)]
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "Thời gian kết thúc là bắt buộc"), DataType(DataType.DateTime)]
    public DateTime? EndAt { get; set; }
}

public sealed class CancelEventViewModel
{
    public Guid EventId { get; set; }

    [Required(ErrorMessage = "Lý do hủy là bắt buộc")]
    public string Reason { get; set; } = "";
}
