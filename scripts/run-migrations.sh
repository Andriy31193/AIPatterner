#!/bin/bash
# Script to run database migrations
cd src/AIPatterner.Api
dotnet ef database update --project ../AIPatterner.Infrastructure

