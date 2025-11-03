# Release Notes

## Version 2.0.0 (Latest)

### Breaking Changes
- The library had known design deficiencies that have been corrected. The library is now more robust and reliable. It is not fully backwards compatible with the v1.x.x release.
  - Settings in the constructor for the receive or send client have been moved into a separate class
  - Old sometimes broken behaviour has been corrected, which can now lead to the client behaving differently. All code using this client should be fully retested.

### New Features
- The client is now able to handle and split up multiple messages in one receive event
- Settings have been moved to a separate setting class
- The library now supports sending and receiving of messages with a length header in big or little endian

### Removed features
- Deadlock simulator has been removed from the integration tests as it only detected deadlocks that where caused by the old insufficient v1.x.x implementation

### Improvements
- Ability to buffer complete messages in memory if multiple messages are received in one receive event.</br>
  This can happen if the sending client sends multiple messages fast enough. See the readme.md under pitfalls for more information.
- Improved unit tests
- Performance optimizations
- Code quality enhancements
- Improved documentation with concrete examples on how to use the client

### Bug Fixes
- Grammar and unclear docuementation fixes
- Addressed stability concerns
- Fixed integration tests to better test the actual use case scenario's the client might encounter in world scenarios

## Version 1.0.0

### New Features
- Sending TCP messages and reading a result from the remote party
- Reading TCP messages and sending back responses
- Handling message length header and/or stx/etx characters to identify a complete message from the remote side