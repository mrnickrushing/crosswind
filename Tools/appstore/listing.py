#!/usr/bin/env python3
"""Fills in Crosswind's App Store listing from docs/appstore/listing.json (run by the Codemagic workflow
`crosswind-listing` in the train app, which holds the App Store Connect key).

  python3 Tools/appstore/listing.py      # needs APP_STORE_CONNECT_PRIVATE_KEY / _KEY_IDENTIFIER / _ISSUER_ID and PyJWT

LISTING_BASE is where docs/appstore/... is read from: this checkout by default, or a URL such as
https://raw.githubusercontent.com/mrnickrushing/crosswind/<branch>/ (the workflow downloads only this script and iap.py).

It sets, on the app info and the version being prepared for submission:
  - categories, the content rights declaration, the age rating answers;
  - en-US subtitle and privacy policy URL; description, keywords, promotional text, support and marketing URLs;
  - copyright; a free price and availability in every territory, if the app has none yet;
  - the screenshot sets (APP_IPHONE_67, APP_IPAD_PRO_3GEN_129), replaced only when the pictures differ.
Every section runs even if another fails; the exit code says whether all of them worked. Safe to run again.
Left for the website: App Privacy answers, the build, the in-app purchases to submit with it, review contact, Submit.
"""
import hashlib
import json
import os
import sys
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iap import BUNDLE_ID, call, pages  # noqa: E402  (same key, same API helpers)

BASE = os.environ.get("LISTING_BASE") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
# Version states that can still be edited.
EDITABLE = ("PREPARE_FOR_SUBMISSION", "DEVELOPER_REJECTED", "REJECTED", "METADATA_REJECTED", "INVALID_BINARY")
# Age rating answers that aren't listed in listing.json: frequency questions are NONE, yes/no questions are no.
FREQUENCY = {"NONE", "INFREQUENT_OR_MILD", "FREQUENT_OR_INTENSE"}
LEAVE = {"kidsAgeBand", "ageRatingOverride", "ageRatingOverrideV2", "koreaAgeRatingOverride", "developerAgeRatingInfoUrl"}


def read(rel):
    if BASE.startswith("http"):
        with urllib.request.urlopen(BASE.rstrip("/") + "/" + rel) as r:
            return r.read()
    with open(os.path.join(BASE, rel), "rb") as f:
        return f.read()


L = json.loads(read("docs/appstore/listing.json"))
failed = []


def section(name):
    """Runs one part of the listing; a failure is reported and the rest still runs."""
    def wrap(fn):
        print("==", name)
        try:
            fn()
        except SystemExit as e:  # call() stops with the API's answer
            print("FAILED", name, "\n", e)
            failed.append(name)
        except Exception as e:  # anything else (a missing key, an upload error): report it, keep going
            print("FAILED", name, "\n", repr(e))
            failed.append(name)
        return fn
    return wrap


def editable(items, key="state"):
    for it in items:
        a = it["attributes"]
        if (a.get(key) or a.get("appStoreState") or a.get("appVersionState")) in EDITABLE:
            return it
    return None


apps = [a for a in call("GET", "/v1/apps?filter[bundleId]=%s" % BUNDLE_ID)["data"] if a["attributes"]["bundleId"] == BUNDLE_ID]
if not apps:
    raise SystemExit("No App Store Connect app with bundle id " + BUNDLE_ID)
APP = apps[0]["id"]
print("APPLE_ID", APP, apps[0]["attributes"]["name"])
infos = pages("/v1/apps/%s/appInfos" % APP)
INFO = editable(infos) or infos[0]
print("appInfo", INFO["id"], INFO["attributes"].get("state") or INFO["attributes"].get("appStoreState"))
versions = pages("/v1/apps/%s/appStoreVersions?filter[platform]=IOS&limit=20" % APP)
VERSION = editable(versions, "appVersionState")
if VERSION is None:
    raise SystemExit("No iOS version is being prepared for submission: %s" % [
        (v["attributes"]["versionString"], v["attributes"].get("appVersionState")) for v in versions])
print("version", VERSION["attributes"]["versionString"], VERSION["id"], VERSION["attributes"].get("appVersionState"))


@section("categories")
def _():
    rel = {}
    for k in ("primaryCategory", "primarySubcategoryOne", "primarySubcategoryTwo", "secondaryCategory"):
        if L.get(k):
            rel[k] = {"data": {"type": "appCategories", "id": L[k]}}
    call("PATCH", "/v1/appInfos/" + INFO["id"], {"data": {"type": "appInfos", "id": INFO["id"], "relationships": rel}})
    print("  " + ", ".join(L[k] for k in ("primaryCategory", "primarySubcategoryOne", "primarySubcategoryTwo", "secondaryCategory") if L.get(k)))


@section("content rights")
def _():
    call("PATCH", "/v1/apps/" + APP, {"data": {"type": "apps", "id": APP,
                                                "attributes": {"contentRightsDeclaration": L["contentRightsDeclaration"]}}})
    print("  " + L["contentRightsDeclaration"])


@section("app info en-US (subtitle, privacy policy)")
def _():
    attrs = {"subtitle": L["subtitle"], "privacyPolicyUrl": L["privacyPolicyUrl"]}
    locs = pages("/v1/appInfos/%s/appInfoLocalizations" % INFO["id"])
    loc = next((x for x in locs if x["attributes"]["locale"] == L["locale"]), None)
    if loc:
        call("PATCH", "/v1/appInfoLocalizations/" + loc["id"], {"data": {"type": "appInfoLocalizations", "id": loc["id"], "attributes": attrs}})
    else:
        attrs.update(locale=L["locale"], name=L["name"])
        call("POST", "/v1/appInfoLocalizations", {"data": {"type": "appInfoLocalizations", "attributes": attrs,
                                                            "relationships": {"appInfo": {"data": {"type": "appInfos", "id": INFO["id"]}}}}})
    print("  subtitle %r" % L["subtitle"])


@section("age rating")
def _():
    decl = call("GET", "/v1/appInfos/%s/ageRatingDeclaration" % INFO["id"])["data"]
    print("  questions: " + ", ".join(sorted(decl["attributes"])))
    want, unknown = {}, []
    for k, v in decl["attributes"].items():
        if k in L["ageRating"]:
            want[k] = L["ageRating"][k]
        elif k in LEAVE:
            continue
        elif isinstance(v, bool) or k in ("gambling", "unrestrictedWebAccess", "lootBox", "seventeenPlus", "advertising",
                                          "ageAssurance", "healthOrWellnessTopics", "messagingAndChat",
                                          "parentalControls", "userGeneratedContent", "socialMedia"):
            want[k] = False
        elif v in FREQUENCY or k in ("contests", "gamblingSimulated") or k.endswith(("Themes", "Violence", "References", "Humor", "Nudity", "Information",
                                           "Simulated", "Contests", "Weapons", "Realistic", "Fantasy", "Sadistic")):
            want[k] = "NONE"
        elif v is None:
            unknown.append(k)
    missing = [k for k in L["ageRating"] if k not in decl["attributes"]]
    if missing:
        print("  not asked by this API version (check on the website): " + ", ".join(missing))
    call("PATCH", "/v1/ageRatingDeclarations/" + decl["id"], {"data": {"type": "ageRatingDeclarations", "id": decl["id"], "attributes": want}})
    for k in sorted(want):
        if want[k] not in ("NONE", False):
            print("  %s: %s" % (k, want[k]))
    if unknown:
        print("  not answered here (answer on the website): " + ", ".join(unknown))


@section("version (copyright)")
def _():
    call("PATCH", "/v1/appStoreVersions/" + VERSION["id"], {"data": {"type": "appStoreVersions", "id": VERSION["id"],
                                                                      "attributes": {"copyright": L["copyright"]}}})
    print("  " + L["copyright"])


VLOC = []


@section("version en-US (description, keywords, promo text, URLs)")
def _():
    attrs = {"description": "\n".join(L["description"]), "keywords": L["keywords"], "promotionalText": L["promotionalText"],
             "supportUrl": L["supportUrl"], "marketingUrl": L["marketingUrl"]}
    assert len(attrs["description"]) <= 4000 and len(attrs["keywords"]) <= 100 and len(attrs["promotionalText"]) <= 170
    locs = pages("/v1/appStoreVersions/%s/appStoreVersionLocalizations" % VERSION["id"])
    loc = next((x for x in locs if x["attributes"]["locale"] == L["locale"]), None)
    if loc:
        call("PATCH", "/v1/appStoreVersionLocalizations/" + loc["id"],
             {"data": {"type": "appStoreVersionLocalizations", "id": loc["id"], "attributes": attrs}})
    else:
        attrs["locale"] = L["locale"]
        loc = call("POST", "/v1/appStoreVersionLocalizations", {"data": {
            "type": "appStoreVersionLocalizations", "attributes": attrs,
            "relationships": {"appStoreVersion": {"data": {"type": "appStoreVersions", "id": VERSION["id"]}}}}})["data"]
    VLOC.append(loc["id"])
    print("  description %d chars, keywords %d" % (len(attrs["description"]), len(attrs["keywords"])))


@section("price (free)")
def _():
    sched = call("GET", "/v1/apps/%s/appPriceSchedule?include=manualPrices" % APP, ok404=True)
    if sched and sched.get("included"):
        print("  already has a price schedule")
        return
    points = pages("/v1/apps/%s/appPricePoints?filter[territory]=USA&limit=200" % APP)
    free = next(p for p in points if float(p["attributes"]["customerPrice"]) == 0)
    call("POST", "/v1/appPriceSchedules", {
        "data": {"type": "appPriceSchedules", "relationships": {
            "app": {"data": {"type": "apps", "id": APP}},
            "baseTerritory": {"data": {"type": "territories", "id": "USA"}},
            "manualPrices": {"data": [{"type": "appPrices", "id": "${free}"}]}}},
        "included": [{"type": "appPrices", "id": "${free}", "attributes": {"startDate": None},
                      "relationships": {"appPricePoint": {"data": {"type": "appPricePoints", "id": free["id"]}}}}]})
    print("  free (USA base)")


@section("availability")
def _():
    have = call("GET", "/v1/apps/%s/appAvailabilityV2" % APP, ok404=True)
    if have and have.get("data"):
        print("  already set")
        return
    ts = [t["id"] for t in pages("/v1/territories?limit=200")]
    call("POST", "/v2/appAvailabilities", {
        "data": {"type": "appAvailabilities", "attributes": {"availableInNewTerritories": True}, "relationships": {
            "app": {"data": {"type": "apps", "id": APP}},
            "territoryAvailabilities": {"data": [{"type": "territoryAvailabilities", "id": "${%s}" % t} for t in ts]}}},
        "included": [{"type": "territoryAvailabilities", "id": "${%s}" % t,
                      "attributes": {"available": True, "releaseDate": None, "preOrderEnabled": False},
                      "relationships": {"territory": {"data": {"type": "territories", "id": t}}}} for t in ts]})
    print("  %d territories" % len(ts))


def upload(set_id, name, data):
    shot = call("POST", "/v1/appScreenshots", {"data": {
        "type": "appScreenshots", "attributes": {"fileName": name, "fileSize": len(data)},
        "relationships": {"appScreenshotSet": {"data": {"type": "appScreenshotSets", "id": set_id}}}}})["data"]
    for op in shot["attributes"]["uploadOperations"]:
        req = urllib.request.Request(op["url"], data=data[op["offset"]:op["offset"] + op["length"]], method=op["method"],
                                     headers={h["name"]: h["value"] for h in op.get("requestHeaders", [])})
        with urllib.request.urlopen(req) as r:
            r.read()
    call("PATCH", "/v1/appScreenshots/" + shot["id"], {"data": {
        "type": "appScreenshots", "id": shot["id"],
        "attributes": {"uploaded": True, "sourceFileChecksum": hashlib.md5(data).hexdigest()}}})


@section("screenshots")
def _():
    if not VLOC:
        raise SystemExit("no version localization")
    sets = pages("/v1/appStoreVersionLocalizations/%s/appScreenshotSets" % VLOC[0])
    for kind, files in L["screenshots"].items():
        s = next((x for x in sets if x["attributes"]["screenshotDisplayType"] == kind), None)
        if s is None:
            s = call("POST", "/v1/appScreenshotSets", {"data": {
                "type": "appScreenshotSets", "attributes": {"screenshotDisplayType": kind},
                "relationships": {"appStoreVersionLocalization": {"data": {"type": "appStoreVersionLocalizations", "id": VLOC[0]}}}}})["data"]
        data = [read("docs/appstore/screens/" + f) for f in files]
        want = [hashlib.md5(d).hexdigest() for d in data]
        have = pages("/v1/appScreenshotSets/%s/appScreenshots" % s["id"])
        if [h["attributes"].get("sourceFileChecksum") for h in have] == want:
            print("  %s: %d already there" % (kind, len(files)))
            continue
        for h in have:
            call("DELETE", "/v1/appScreenshots/" + h["id"])
        for f, d in zip(files, data):
            upload(s["id"], f, d)
        print("  %s: uploaded %d" % (kind, len(files)))


print("LISTING", "done" if not failed else "FAILED: " + ", ".join(failed))
sys.exit(1 if failed else 0)
