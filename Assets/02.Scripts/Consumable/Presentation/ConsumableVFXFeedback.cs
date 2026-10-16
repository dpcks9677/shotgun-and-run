using System.Collections;
using UnityEngine;

namespace Consumable
{
    public class ConsumableVFXFeedback : MonoBehaviour
    {
        [SerializeField] private GameObject _heal;
        [SerializeField] private GameObject _adrenaline;

        public void PlayHeal()
        {
            GameObject healObject =
                Instantiate(_heal, transform.position, Quaternion.identity, transform.parent);
            healObject.SetActive(true);
        }

        public IEnumerator PlayAdrenaline()
        {
            GameObject adrenalineObject = Instantiate(_adrenaline, transform.position + Vector3.up * 1f, 
                Quaternion.identity, transform.parent);
            adrenalineObject.SetActive(true);
            yield return new WaitForSeconds(10f);
            
            Destroy(adrenalineObject);
        }
    }
}
