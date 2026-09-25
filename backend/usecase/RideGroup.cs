namespace Usecase.RideGroup
{
    public interface IRideGroupRepository
    {
        // 確定済み・運行中の便が残っているか（候補の段階は含めない）
        Task<bool> HasUnfinishedByDriverAsync(Guid driverId);
    }
}
