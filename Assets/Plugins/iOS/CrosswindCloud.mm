// iCloud key-value storage for the Crosswind save (CloudSave.cs). The save is a few kilobytes, well inside the
// store's 1 MB; iOS uploads it in the background and brings it down on the player's other devices.
#import <Foundation/Foundation.h>

static char *CWCopy(NSString *s)
{
    if (s == nil) return NULL;
    const char *u = [s UTF8String];
    char *out = (char *)malloc(strlen(u) + 1);
    strcpy(out, u);
    return out; // the C# marshaller frees it
}

extern "C"
{
    // Signed in to iCloud on this device (the store works only then).
    bool CWCloudAvailable()
    {
        return [[NSFileManager defaultManager] ubiquityIdentityToken] != nil;
    }

    void CWCloudSet(const char *key, const char *value)
    {
        NSUbiquitousKeyValueStore *kv = [NSUbiquitousKeyValueStore defaultStore];
        [kv setString:[NSString stringWithUTF8String:value] forKey:[NSString stringWithUTF8String:key]];
        [kv synchronize];
    }

    char *CWCloudGet(const char *key)
    {
        NSUbiquitousKeyValueStore *kv = [NSUbiquitousKeyValueStore defaultStore];
        return CWCopy([kv stringForKey:[NSString stringWithUTF8String:key]]);
    }

    void CWCloudRemove(const char *key)
    {
        NSUbiquitousKeyValueStore *kv = [NSUbiquitousKeyValueStore defaultStore];
        [kv removeObjectForKey:[NSString stringWithUTF8String:key]];
        [kv synchronize];
    }

    // Pulls what iCloud has now (call at launch, before reading).
    void CWCloudSync()
    {
        [[NSUbiquitousKeyValueStore defaultStore] synchronize];
    }
}
