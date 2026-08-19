pipeline {
    agent any

    environment {
        // 유니티 경로 및 워크스페이스
        UNITY_PATH = 'E:\\UnityPrograms\\6000.3.8f1\\Editor\\Unity.exe' 
        PROJECT_PATH = "${WORKSPACE}"
        LOG_PATH = "${WORKSPACE}\\build.log"
        
        // S3 버킷 설정 (플랫폼별 경로 분리)
        S3_BUCKET_WINDOWS = 's3://treasurehunter-addressables/content/StandaloneWindows64'
        S3_BUCKET_ANDROID = 's3://treasurehunter-addressables/content/Android'
        
        // bin 파일 보관용 최상위 경로
        S3_STATE_BUCKET = 's3://treasurehunter-addressables/addressables-state' 
        
        // Addressables 기본 빌드 결과물 플랫폼별 경로
        ADDRESSABLES_OUTPUT_WINDOWS = "${WORKSPACE}\\ServerData\\StandaloneWindows64"
        ADDRESSABLES_OUTPUT_ANDROID = "${WORKSPACE}\\ServerData\\Android"
        
        // bin 파일이 생성/요구되는 유니티 내부 경로 (플랫폼별)
        STATE_FILE_WINDOWS = "${WORKSPACE}\\Assets\\AddressableAssetsData\\Windows64\\addressables_content_state.bin"
        STATE_FILE_ANDROID = "${WORKSPACE}\\Assets\\AddressableAssetsData\\Android\\addressables_content_state.bin"
    }

    parameters {
        choice(
            name: 'BUILD_TYPE',
            choices: [
                'WindowsAddressblesOnly', 
                'AndroidAddressablesOnly', 
                'WindowsFullBuild', 
                'AndroidFullBuild', 
                'FullBuild'
            ],
            description: '실행할 빌드 타입을 선택하세요.'
        )
        string(
            name: 'APP_VERSION',
            defaultValue: '1.0.0',
            description: '빌드/업데이트할 클라이언트 버전을 입력하세요 (예: 1.0.0)'
        )
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
                echo "선택된 빌드 타입: ${params.BUILD_TYPE}, 타겟 버전: ${params.APP_VERSION}"
            }
        }

        // ---------------------------------------------------------
        // 1. Addressables 빌드 (State 파일 복원 및 백업)
        // ---------------------------------------------------------
        stage('Build Windows Addressables') {
            when {
                anyOf {
                    expression { params.BUILD_TYPE == 'WindowsAddressblesOnly' }
                    expression { params.BUILD_TYPE == 'WindowsFullBuild' }
                    expression { params.BUILD_TYPE == 'FullBuild' }
                }
            }
            steps {
                script {
                    def s3StatePath = "${S3_STATE_BUCKET}/${params.APP_VERSION}/Windows64/addressables_content_state.bin"

                    if (params.BUILD_TYPE == 'WindowsAddressblesOnly') {
                        echo "Windows - 버전 ${params.APP_VERSION}의 state bin 파일을 가져옵니다."
                        bat "aws s3 cp \"${s3StatePath}\" \"${STATE_FILE_WINDOWS}\""
                        
                        echo "Windows - Update Previous Build 시작..."
                        bat "\"${UNITY_PATH}\" -quit -batchmode -projectPath \"${PROJECT_PATH}\" -executeMethod Builder.UpdateWindowsAddressables -logFile \"${LOG_PATH}\""
                    } else {
                        echo "Windows - New Addressables Build 시작..."
                        bat "\"${UNITY_PATH}\" -quit -batchmode -projectPath \"${PROJECT_PATH}\" -executeMethod Builder.BuildWindowsAddressables -logFile \"${LOG_PATH}\""
                        
                        echo "Windows - 생성된 state bin 파일을 S3의 ${params.APP_VERSION} 폴더에 백업합니다."
                        bat "aws s3 cp \"${STATE_FILE_WINDOWS}\" \"${s3StatePath}\""
                    }
                }
            }
        }

        stage('Build Android Addressables') {
            when {
                anyOf {
                    expression { params.BUILD_TYPE == 'AndroidAddressablesOnly' }
                    expression { params.BUILD_TYPE == 'AndroidFullBuild' }
                    expression { params.BUILD_TYPE == 'FullBuild' }
                }
            }
            steps {
                script {
                    def s3StatePathAndroid = "${S3_STATE_BUCKET}/${params.APP_VERSION}/Android/addressables_content_state.bin"

                    if (params.BUILD_TYPE == 'AndroidAddressablesOnly') {
                        echo "Android - 버전 ${params.APP_VERSION}의 state bin 파일을 가져옵니다."
                        bat "aws s3 cp \"${s3StatePathAndroid}\" \"${STATE_FILE_ANDROID}\""
                        
                        echo "Android - Update Previous Build 시작..."
                        bat "\"${UNITY_PATH}\" -quit -batchmode -projectPath \"${PROJECT_PATH}\" -executeMethod Builder.UpdateAndroidAddressables -logFile \"${LOG_PATH}\""
                    } else {
                        echo "Android - New Addressables Build 시작..."
                        bat "\"${UNITY_PATH}\" -quit -batchmode -projectPath \"${PROJECT_PATH}\" -executeMethod Builder.BuildAndroidAddressables -logFile \"${LOG_PATH}\""
                        
                        echo "Android - 생성된 state bin 파일을 S3의 ${params.APP_VERSION} 폴더에 백업합니다."
                        bat "aws s3 cp \"${STATE_FILE_ANDROID}\" \"${s3StatePathAndroid}\""
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // 2. 클라이언트 빌드
        // ---------------------------------------------------------
        stage('Build Windows Client') {
            when {
                anyOf {
                    expression { params.BUILD_TYPE == 'WindowsFullBuild' }
                    expression { params.BUILD_TYPE == 'FullBuild' }
                }
            }
            steps {
                echo "Windows 클라이언트 빌드를 시작합니다..."
                bat "\"${UNITY_PATH}\" -quit -batchmode -projectPath \"${PROJECT_PATH}\" -executeMethod Builder.BuildWindowsClient -logFile \"${LOG_PATH}\""
            }
        }

        stage('Build Android Client') {
            when {
                anyOf {
                    expression { params.BUILD_TYPE == 'AndroidFullBuild' }
                    expression { params.BUILD_TYPE == 'FullBuild' }
                }
            }
            steps {
                echo "Android 클라이언트 (APK/AAB) 빌드를 시작합니다..."
                bat "\"${UNITY_PATH}\" -quit -batchmode -projectPath \"${PROJECT_PATH}\" -executeMethod Builder.BuildAndroidClient -logFile \"${LOG_PATH}\""
            }
        }

        // ---------------------------------------------------------
        // 3. AWS S3 업로드 (결과물 플랫폼 분기 배포)
        // ---------------------------------------------------------
        stage('Upload to S3') {
            steps {
                script {
                    // Windows 빌드가 포함된 파라미터(3가지)일 때 업로드
                    if (params.BUILD_TYPE == 'WindowsAddressblesOnly' || params.BUILD_TYPE == 'WindowsFullBuild' || params.BUILD_TYPE == 'FullBuild') {
                        echo "S3 버킷으로 Windows Addressables 번들 업로드를 시작합니다..."
                        bat "aws s3 sync \"${ADDRESSABLES_OUTPUT_WINDOWS}\" \"${S3_BUCKET_WINDOWS}\" --exclude \"*\" --include \"*.hash\" --include \"*.bin\" --include \"*.json\" --include \"*.bundle\""
                    }
                    
                    // Android 빌드가 포함된 파라미터(3가지)일 때 업로드
                    if (params.BUILD_TYPE == 'AndroidAddressablesOnly' || params.BUILD_TYPE == 'AndroidFullBuild' || params.BUILD_TYPE == 'FullBuild') {
                        echo "S3 버킷으로 Android Addressables 번들 업로드를 시작합니다..."
                        bat "aws s3 sync \"${ADDRESSABLES_OUTPUT_ANDROID}\" \"${S3_BUCKET_ANDROID}\" --exclude \"*\" --include \"*.hash\" --include \"*.bin\" --include \"*.json\" --include \"*.bundle\""
                    }
                }
            }
        }
    }

    post {
        always {
            echo "파이프라인 실행 완료. 로그 파일을 아카이브합니다."
            archiveArtifacts artifacts: 'build.log', allowEmptyArchive: true
        }
        success {
            echo "🎉 모든 빌드 스테이지가 성공적으로 완료되었습니다!"
            
            // 클라이언트 빌드(exe, apk) 결과물을 Jenkins 대시보드에서 다운로드할 수 있도록 보관합니다.
            archiveArtifacts artifacts: 'Builds/**/*', allowEmptyArchive: true
        }
        failure {
            echo "❌ 빌드 중 오류가 발생했습니다. build.log를 확인하세요."
        }
    }
}