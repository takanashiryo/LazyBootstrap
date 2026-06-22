namespace LazyBootstrap.Features.Launch
{
    public interface ILaunchWorkflowObserver
    {
        void OnLaunchStateChanged(LaunchState state);

        void OnLaunchLogVisibilityChanged(LaunchState state);

        void OnLaunchLogChanged(LaunchState state);

        void OnLaunchMessageChanged(LaunchMessage message);
    }
}
