// Photo mode's SHARE (Hud.Photo.cs): the system share sheet with the picture the game just took, so the player can
// save it to Photos (NSPhotoLibraryAddUsageDescription, written by IosCapabilities), message it or post it.
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController();

extern "C"
{
    void CWShareImage(const char *path, const char *text)
    {
        NSString *file = [NSString stringWithUTF8String:path];
        UIImage *image = [UIImage imageWithContentsOfFile:file];
        if (image == nil) return;
        NSMutableArray *items = [NSMutableArray arrayWithObject:image];
        if (text != NULL && strlen(text) > 0) [items addObject:[NSString stringWithUTF8String:text]];
        UIActivityViewController *sheet = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        UIViewController *host = UnityGetGLViewController();
        // On an iPad the sheet is a popover and needs somewhere to point: the middle of the screen's foot.
        if (sheet.popoverPresentationController != nil)
        {
            sheet.popoverPresentationController.sourceView = host.view;
            sheet.popoverPresentationController.sourceRect = CGRectMake(host.view.bounds.size.width * 0.5, host.view.bounds.size.height - 40, 1, 1);
            sheet.popoverPresentationController.permittedArrowDirections = UIPopoverArrowDirectionDown;
        }
        [host presentViewController:sheet animated:YES completion:nil];
    }
}
