import path from 'node:path';
import { defineConfig } from '@playwright/test';
export default defineConfig({testDir:'./tests/browser',workers:1,timeout:30000,use:{baseURL:'http://127.0.0.1:8080',browserName:'chromium',locale:'en-US',viewport:{width:1280,height:900}},webServer:{command:'dotnet src/bin/Release/net10.0/App.dll',url:'http://127.0.0.1:8080/api/health',reuseExistingServer:false,env:{ASPNETCORE_CONTENTROOT:path.resolve('src'),ASPNETCORE_URLS:'http://127.0.0.1:8080',ASPNETCORE_ENVIRONMENT:'Testing',TestFixtures:'true',DATA_DIR:'.data/browser'},timeout:30000}});
