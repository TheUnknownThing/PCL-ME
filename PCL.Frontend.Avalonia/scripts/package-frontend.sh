#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit 2>/dev/null || true

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
project_path="${repo_root}/PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj"
artifact_root="${ARTIFACT_ROOT:-${repo_root}/artifacts/frontend-packages}"
configuration="${CONFIGURATION:-Release}"
app_name="${APP_NAME:-PCL-ME}"
bundle_identifier="${BUNDLE_IDENTIFIER:-org.pcl.me.frontend}"
app_version="${APP_VERSION:-$(date +%Y.%m.%d)}"
publish_mode="${PUBLISH_MODE:-self-contained}"
executable_name="PCL.Frontend.Avalonia"
mac_launcher_name="PCLLauncher"
linux_launcher_script="launch-pcl-me.sh"
windows_launcher_script="Launch PCL-ME.vbs"
icon_png="${repo_root}/PCL.Frontend.Avalonia/Assets/icon.png"

get_default_rids() {
  case "$(uname -s)" in
    Darwin)
      printf '%s\n' osx-arm64 linux-x64 win-x64
      ;;
    Linux)
      printf '%s\n' linux-x64 win-x64
      ;;
    MINGW*|MSYS*|CYGWIN*)
      printf '%s\n' win-x64
      ;;
    *)
      printf '%s\n' linux-x64
      ;;
  esac
}

default_rids=()
while IFS= read -r rid; do
  default_rids+=("$rid")
done < <(get_default_rids)
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

create_zip_archive() {
  local source_dir="$1"
  local archive_path="$2"

  python3 - "$source_dir" "$archive_path" <<'PY'
import os
import sys
import zipfile

source_dir = os.path.abspath(sys.argv[1])
archive_path = os.path.abspath(sys.argv[2])
archive_root = os.path.dirname(source_dir)

with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for current_root, dir_names, file_names in os.walk(source_dir):
        dir_names.sort()
        file_names.sort()
        for file_name in file_names:
            file_path = os.path.join(current_root, file_name)
            relative_path = os.path.relpath(file_path, archive_root)
            archive.write(file_path, relative_path)
PY
}

build_publish() {
  local rid="$1"
  local output_dir="$2"
  local self_contained="false"
  local publish_single_file="false"
  local include_all_content_for_self_extract="false"
  local enable_single_file_compression="false"
  if [[ "${publish_mode}" == "self-contained" ]]; then
    self_contained="true"
  fi
  if [[ "$rid" == win-* || "$rid" == linux-* ]]; then
    publish_single_file="true"
    include_all_content_for_self_extract="true"
    enable_single_file_compression="true"
  fi

  dotnet publish "$project_path" \
    -c "$configuration" \
    -r "$rid" \
    --self-contained "$self_contained" \
    -p:PublishSingleFile="$publish_single_file" \
    -p:IncludeAllContentForSelfExtract="$include_all_content_for_self_extract" \
    -p:EnableCompressionInSingleFile="$enable_single_file_compression" \
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
  local plist_template="${repo_root}/PCL.Frontend.Avalonia/packaging/Info.plist.template"
  local plist_path="${app_dir}/Contents/Info.plist"
  local resources_dir="${app_dir}/Contents/Resources"
  local macos_dir="${app_dir}/Contents/MacOS"
  local archive_path="${rid_root}/$(echo "${app_name}" | tr ' ' '-')-${rid}.zip"

  prepare_directory "$app_dir"
  mkdir -p "$resources_dir" "$macos_dir"
  copy_tree "$publish_dir" "$macos_dir"
  write_text_file "${macos_dir}/${mac_launcher_name}" "#!/bin/sh
script_dir=\"\$(cd \"\$(dirname \"\$0\")\" && pwd)\"
exec \"\${script_dir}/${executable_name}\" app \"\$@\"
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
  local archive_path="${rid_root}/$(basename "$package_dir").tar.gz"
  local published_executable="${publish_dir}/${executable_name}"

  if [[ ! -f "$published_executable" ]]; then
    echo "Published executable not found: $published_executable" >&2
    exit 1
  fi

  prepare_directory "$package_dir"
  cp "$published_executable" "${package_dir}/${executable_name}"
  cp "$icon_png" "${package_dir}/icon.png"
  chmod +x "${package_dir}/${executable_name}"
  tar -C "$rid_root" -czf "$archive_path" "$(basename "$package_dir")"
  echo "$archive_path"
}

package_windows() {
  local rid="$1"
  local publish_dir="$2"
  local rid_root="$3"
  local package_path="${rid_root}/$(echo "${app_name}" | tr ' ' '-')-${rid}.exe"
  local published_executable="${publish_dir}/${executable_name}.exe"

  if [[ ! -f "$published_executable" ]]; then
    echo "Published executable not found: $published_executable" >&2
    exit 1
  fi

  cp "$published_executable" "$package_path"
  echo "$package_path"
}

require_tool dotnet
require_tool tar
require_tool sed
require_tool python3

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
