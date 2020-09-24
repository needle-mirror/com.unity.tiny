uniform float4x4 u_bone_matrices[16];

float4x4 mtxForGPUSkinning(float4 _boneWeight, float4 _boneIndices)
{
    float4x4 mat_x = _boneWeight.x * u_bone_matrices[(int)_boneIndices.x];
    float4x4 mat_y = _boneWeight.y * u_bone_matrices[(int)_boneIndices.y];
    float4x4 mat_z = _boneWeight.z * u_bone_matrices[(int)_boneIndices.z];
    float4x4 mat_w = _boneWeight.w * u_bone_matrices[(int)_boneIndices.w];
    return mat_x + mat_y + mat_z + mat_w;
}