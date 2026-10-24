# Code signing policy

**Status:** SignPath Foundation application is being prepared. The project is not yet approved and release binaries are **unsigned**.

After approval: Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## Project and responsibilities

- Source repository: https://github.com/mikeauftour/nanoSONAR
- Release page: https://github.com/mikeauftour/nanoSONAR/releases
- Author, committer, reviewer and signing approver: [mikeauftour](https://github.com/mikeauftour)
- Contributions from others must be reviewed before merging.
- The maintainer explicitly approves each signing request.
- All project maintainers must use MFA for GitHub and SignPath.

## Security and privacy

nanoSONAR is a Windows 11 desktop MIDI controller for SteelSeries GG Sonar. It connects to locally running SteelSeries GG/Sonar services via loopback and stores user settings locally. The project does not intentionally send user information to external systems. SteelSeries GG and other separately installed software are governed by their own privacy practices.

nanoSONAR can configure Windows autostart when selected by the user. This setting can be disabled within the app. Uninstall the portable application by disabling autostart, exiting the app and deleting its folder; separately stored configuration files may be removed manually.

## Builds and signing

Windows builds run through public GitHub Actions from repository source and scripts. Release packages are currently unsigned. The project will not describe a binary as signed until its signature has been verified. Signing configuration and release metadata will be established with SignPath following acceptance. Signing is subject to SignPath Foundation approval and its terms.
