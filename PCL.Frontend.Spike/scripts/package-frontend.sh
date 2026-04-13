#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
project_path="${repo_root}/PCL.Frontend.Spike/PCL.Frontend.Spike.csproj"
artifact_root="${ARTIFACT_ROOT:-${repo_root}/artifacts/frontend-packages}"
configuration="${CONFIGURATION:-Release}"
app_name="${APP_NAME:-Plain Craft Launcher Community Edition}"
bundle_identifier="${BUNDLE_IDENTIFIER:-org.pcl.community.frontend}"
app_version="${APP_VERSION:-$(date +%Y.%m.%d)}"
publish_mode="${PUBLISH_MODE:-self-contained}"
executable_name="PCL.Frontend.Spike"
mac_launcher_name="PCLLauncher"
linux_launcher_script="launch-pcl-ce.sh"
windows_launcher_script="Launch Plain Craft Launcher Community Edition.vbs"
icon_png="${repo_root}/Plain Craft Launcher 2/Images/icon.png"

default_rids=(osx-arm64 linux-x64 win-x64)
if [[ "$#" -gt 0 ]]; then
  rids=("$@")
else
  rids=("${default_rids[@]}")
fi

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required tool: $1" >&2
    exit 1
  fi
}

prepare_directory() {
  local path="$1"
  rm -rf "$path"
  mkdir -p "$path"
}

copy_tree() {
  local source="$1"
  local target="$2"
  mkdir -p "$target"
  cp -R "${source}/." "$target/"
}

build_publish() {
  local rid="$1"
  local output_dir="$2"
  local self_contained="false"
  if [[ "${publish_mode}" == "self-contained" ]]; then
    self_contained="true"
  fi

  dotnet publish "$project_path" \
    -c "$configuration" \
    -r "$rid" \
    --self-contained "$self_contained" \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=false \
    -o "$output_dir"
}

write_text_file() {
  local path="$1"
  local content="$2"
  printf "%s" "$content" > "$path"
}

create_mac_icon() {
  local target_icns="$1"
  local iconset_dir="$2/AppIcon.iconset"

  require_tool iconutil
  require_tool sips

  prepare_directory "$iconset_dir"
  sips -z 16 16 "$icon_png" --out "$iconset_dir/icon_16x16.png" >/dev/null
  sips -z 32 32 "$icon_png" --out "$iconset_dir/icon_16x16@2x.png" >/dev/null
  sips -z 32 32 "$icon_png" --out "$iconset_dir/icon_32x32.png" >/dev/null
  sips -z 64 64 "$icon_png" --out "$iconset_dir/icon_32x32@2x.png" >/dev/null
  sips -z 128 128 "$icon_png" --out "$iconset_dir/icon_128x128.png" >/dev/null
  sips -z 256 256 "$icon_png" --out "$iconset_dir/icon_128x128@2x.png" >/dev/null
  sips -z 256 256 "$icon_png" --out "$iconset_dir/icon_256x256.png" >/dev/null
  sips -z 512 512 "$icon_png" --out "$iconset_dir/icon_256x256@2x.png" >/dev/null
  sips -z 512 512 "$icon_png" --out "$iconset_dir/icon_512x512.png" >/dev/null
  cp "$icon_png" "$iconset_dir/icon_512x512@2x.png"
  iconutil -c icns "$iconset_dir" -o "$target_icns"
}

package_macos() {
  local rid="$1"
  local publish_dir="$2"
  local rid_root="$3"
  local temp_dir="$4"
  local app_dir="${rid_root}/${app_name}.app"
  local plist_template="${repo_root}/PCL.Frontend.Spike/packaging/Info.plist.template"
  local plist_path="${app_dir}/Contents/Info.plist"
  local resources_dir="${app_dir}/Contents/Resources"
  local macos_dir="${app_dir}/Contents/MacOS"
  local archive_path="${rid_root}/$(echo "${app_name}" | tr ' ' '-')-${rid}.zip"

  prepare_directory "$app_dir"
  mkdir -p "$resources_dir" "$macos_dir"
  copy_tree "$publish_dir" "$macos_dir"
  write_text_file "${macos_dir}/${mac_launcher_name}" "#!/bin/sh
script_dir=\"\$(cd \"\$(dirname \"\$0\")\" && pwd)\"
exec \"\${script_dir}/${executable_name}\" app --host-env true \"\$@\"
"
  create_mac_icon "${resources_dir}/AppIcon.icns" "$temp_dir"
  sed \
    -e "s|__APP_NAME__|${app_name}|g" \
    -e "s|__LAUNCHER_NAME__|${mac_launcher_name}|g" \
    -e "s|__BUNDLE_IDENTIFIER__|${bundle_identifier}|g" \
    -e "s|__APP_VERSION__|${app_version}|g" \
    "$plist_template" > "$plist_path"
  chmod +x "${macos_dir}/${executable_name}" "${macos_dir}/${mac_launcher_name}"
  ditto -c -k --sequesterRsrc --keepParent "$app_dir" "$archive_path"
  echo "$archive_path"
}

package_linux() {
  local rid="$1"
  local publish_dir="$2"
  local rid_root="$3"
  local package_dir="${rid_root}/$(echo "${app_name}" | tr ' ' '-')-${rid}"
  local desktop_template="${repo_root}/PCL.Frontend.Spike/packaging/pcl-ce.desktop.template"
  local archive_path="${rid_root}/$(basename "$package_dir").tar.gz"

  prepare_directory "$package_dir"
  copy_tree "$publish_dir" "$package_dir"
  cp "$icon_png" "${package_dir}/icon.png"
  write_text_file "${package_dir}/${linux_launcher_script}" "#!/usr/bin/env bash
set -euo pipefail
script_dir=\"\$(cd \"\$(dirname \"\${BASH_SOURCE[0]}\")\" && pwd)\"
exec \"\${script_dir}/${executable_name}\" app --host-env true \"\$@\"
"
  sed \
    -e "s|__APP_NAME__|${app_name}|g" \
    -e "s|__LAUNCHER_SCRIPT__|${linux_launcher_script}|g" \
    "$desktop_template" > "${package_dir}/Plain Craft Launcher Community Edition.desktop"
  chmod +x "${package_dir}/${executable_name}" "${package_dir}/${linux_launcher_script}" "${package_dir}/Plain Craft Launcher Community Edition.desktop"
  tar -C "$rid_root" -czf "$archive_path" "$(basename "$package_dir")"
  echo "$archive_path"
}

package_windows() {
  local rid="$1"
  local publish_dir="$2"
  local rid_root="$3"
  local package_dir="${rid_root}/$(echo "${app_name}" | tr ' ' '-')-${rid}"
  local archive_path="${rid_root}/$(basename "$package_dir").zip"

  prepare_directory "$package_dir"
  copy_tree "$publish_dir" "$package_dir"
  write_text_file "${package_dir}/${windows_launcher_script}" "Set shell = CreateObject(\"WScript.Shell\")
Set fileSystem = CreateObject(\"Scripting.FileSystemObject\")
scriptDir = fileSystem.GetParentFolderName(WScript.ScriptFullName)
shell.Run Chr(34) & scriptDir & \"\\\\PCL.Frontend.Spike.exe\" & Chr(34) & \" app --host-env true\", 0
"
  (
    cd "$rid_root"
    zip -qry "$archive_path" "$(basename "$package_dir")"
  )
  echo "$archive_path"
}

require_tool dotnet
require_tool zip
require_tool tar
require_tool sed

prepare_directory "$artifact_root"

printf "Packaging %s for: %s\n" "$app_name" "${rids[*]}"

for rid in "${rids[@]}"; do
  rid_root="${artifact_root}/${rid}"
  publish_dir="${rid_root}/publish"
  temp_dir="${rid_root}/tmp"
  prepare_directory "$rid_root"
  mkdir -p "$temp_dir"

  echo
  echo "==> Publishing ${rid}"
  build_publish "$rid" "$publish_dir"

  echo "==> Packaging ${rid}"
  case "$rid" in
    osx-*)
      archive_path="$(package_macos "$rid" "$publish_dir" "$rid_root" "$temp_dir")"
      ;;
    linux-*)
      archive_path="$(package_linux "$rid" "$publish_dir" "$rid_root")"
      ;;
    win-*)
      archive_path="$(package_windows "$rid" "$publish_dir" "$rid_root")"
      ;;
    *)
      echo "Unsupported RID: ${rid}" >&2
      exit 1
      ;;
  esac

  echo "Created package: ${archive_path}"
done

echo
echo "All packages are available under ${artifact_root}"
