import argparse, hashlib, json, platform, re, unicodedata
from pathlib import Path
import joblib
import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity
import sklearn


def normalize(text):
    decomposed = unicodedata.normalize('NFKD', text.lower())
    ascii_text = ''.join(c for c in decomposed if c.isascii() and (c.isalnum() or c.isspace()))
    return re.sub(r'\s+', ' ', ascii_text).strip()


def document(row):
    return normalize(' '.join([str(row.get('title', ''))] * 3 + [str(row.get('category', ''))] * 2 + [str(row.get(k, '')) for k in ('description', 'mode', 'condition')]))


def train(source, destination, private_dir):
    rows = json.loads(Path(source).read_text(encoding='utf-8-sig'))
    rows = [r for r in rows if r.get('available', True)]
    if len(rows) < 3:
        raise ValueError('Se necesitan al menos tres publicaciones elegibles.')
    texts = [document(row) for row in rows]
    vectorizer = TfidfVectorizer(analyzer='char', ngram_range=(3, 5), lowercase=False,
                                 norm='l2', min_df=1, max_features=12000, dtype=np.float64)
    matrix = vectorizer.fit_transform(texts)
    examples = [('laptop para estudiar', 'Computación y accesorios'), ('bicicleta deporte', 'Deportes'),
                ('libros lectura', 'Libros'), ('ropa abrigo', 'Ropa'),
                ('mueble escritorio silla', 'Muebles'), ('cuaderno oficina estudiar', 'Oficina y útiles')]
    evaluations, parity = [], []
    labels = [r['category'] for r in rows]
    for query, category in examples:
        scores = cosine_similarity(vectorizer.transform([normalize(query)]), matrix)[0]
        order = sorted(range(len(rows)), key=lambda i: (-scores[i], rows[i]['id']))[:5]
        relevance = np.array([float(labels[i] == category) for i in order])
        relevant = sum(label == category for label in labels)
        ideal = np.array([1.0] * min(5, relevant) + [0.0] * max(0, 5-relevant))
        discount = 1 / np.log2(np.arange(2, 7))
        ndcg = float((relevance * discount[:len(relevance)]).sum() / (ideal * discount).sum()) if relevant else 0.0
        evaluations.append({'query': query, 'relevantCategory': category, 'precisionAt5': float(relevance.mean()), 'ndcgAt5': ndcg})
        parity.append({'query': query, 'scores': {rows[i]['id']: float(scores[i]) for i in range(len(rows))}})
    vocabulary = {key: int(value) for key, value in sorted(vectorizer.vocabulary_.items(), key=lambda pair: pair[1])}
    payload = {'schema': 1, 'algorithm': 'scikit-learn TfidfVectorizer(char 3..5) + cosine_similarity',
               'normalization': 'NFKD-ascii-alphanumeric-whitespace-v1',
               'sklearnVersion': sklearn.__version__, 'pythonVersion': platform.python_version(),
               'corpusSha256': hashlib.sha256('\n'.join(texts).encode()).hexdigest(),
               'fieldWeights': {'title': 3, 'category': 2, 'description': 1, 'mode': 1, 'condition': 1}, 'documents': len(rows), 'vocabulary': vocabulary, 'idf': vectorizer.idf_.tolist(),
               'corpus': [{'id': r['id'], 'text': text} for r, text in zip(rows, texts)],
               'parity': parity}
    dest = Path(destination); dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(payload, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    out = Path(private_dir); out.mkdir(parents=True, exist_ok=True)
    joblib.dump({'vectorizer': vectorizer, 'matrix': matrix, 'ids': [r['id'] for r in rows]}, out / 'tfidf.joblib')
    report = {'dataset': 'Publicaciones DEMO sintéticas; evaluación de recuperación, no prueba con usuarios reales',
              'documents': len(rows), 'features': len(vocabulary), 'queries': evaluations,
              'meanPrecisionAt5': float(np.mean([e['precisionAt5'] for e in evaluations])),
              'meanNdcgAt5': float(np.mean([e['ndcgAt5'] for e in evaluations])),
              'coverageAt5': len({i for p in parity for i in sorted(p['scores'], key=lambda i: (-p['scores'][i], i))[:5]}) / len(rows),
              'training': 'Ajuste TF-IDF una vez fuera del servidor HTTP; corpus completo de candidatos públicos',
              'limitations': 'Relevancia sintética por categoría; no demuestra preferencias reales ni satisfacción.'}
    (out / 'evaluation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: report[k] for k in ('documents', 'features', 'meanPrecisionAt5', 'meanNdcgAt5', 'coverageAt5')}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', default='SegundaOportunidad/App_Data/recommender.json')
    parser.add_argument('--artifacts', default='ml/artifacts')
    args = parser.parse_args()
    train(args.input, args.output, args.artifacts)
